using UnityEngine;
using UnityEngine.InputSystem;

namespace TinyWorld
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class WandererMotor : MonoBehaviour
    {
        public Transform heading, visual;
        public WandererFeedback feedback;
        public LayerMask worldMask = 1;
        public float walkSpeed = 5.6f, sprintSpeed = 10.2f, jumpHeight = 2.5f;
        public float wallRunSpeed = 10.5f, wallRunDuration = 1.65f, swimSpeed = 5.5f;
        public Material ropeMaterial;
        public bool ControlsEnabled { get; set; }
        public bool Grounded { get; private set; }
        public bool Traversing { get; private set; }
        public bool Hanging { get; private set; }
        public bool WallRunning { get; private set; }
        public bool Swinging => anchor != null;
        public bool Swimming { get; private set; }
        public bool Dashing => dashTime > 0;
        public bool Crouched { get; private set; }
        public StickyHand Hand { get; private set; }
        public bool Grappling => Hand && Hand.Attached;
        public Vector3 TetherOrigin => transform.position+Vector3.up*(body.height*.58f);
        bool grappleFlight;
        public bool VineClimbing => vine;
        ClimbableVine vine;
        public Vector3 LedgeNormal => ledgeNormal;
        public bool Leaping { get; private set; }
        public bool Zipping { get; private set; }
        public float TraverseProgress => Traversing ? Mathf.Clamp01(traverseTime / traverseDuration) : 0;
        public float RollProgress => rollTime > 0 ? 1 - rollTime / RollDuration : 0;
        public const float RollDuration = .46f;
        float rollTime, carrySpeed, leapTime, leapDuration, leapArc = .35f;
        Vector3 carryDir, leapStart, leapEnd, leapTop, leapNormal, leapWall;
        const float StandingHeight=1.8f, CrouchHeight=1.02f;
        static readonly float[] MantleLifts={.07f,.3f,.6f};
        static readonly float[] LandingSides={0,.35f,-.35f,.7f,-.7f};
        static readonly float[] LedgeInsets={.06f,.25f,.5f};
        static readonly float[] LandingDepths={.42f,.7f,1f,1.35f,1.7f};
        static readonly float[] Retreats={0,.2f,.4f,.65f,.9f};
        public Vector3 WallNormal { get; private set; }
        public Vector3 Velocity => velocity;
        public float Speed => new Vector2(velocity.x, velocity.z).magnitude;
        public float VerticalSpeed => velocity.y;
        public string Action { get; private set; } = "Explore";
        public string Hint { get; private set; } = "";
        public int Jumps { get; private set; }
        public int Traversals { get; private set; }
        public int Respawns { get; private set; }
        public int WallJumps { get; private set; }
        public int WallRuns { get; private set; }
        public int Grabs { get; private set; }
        public int Swings { get; private set; }
        public int Dashes { get; private set; }
        CharacterController body;
        Vector3 velocity, checkpoint, traverseStart, traverseEnd, traverseLiftStart, ledgeTop, ledgeNormal, dashDirection;
        float traverseLiftY;
        bool traverseFromHang;
        float hangTime, climbRetry;
        bool climbBlocked, manualCrouch, cornering, cornerClimbRequested;
        float crouchGrace, cornerTime, cornerSign;
        Vector3 ledgeWallPoint, cornerCenter, cornerFrom, cornerTo, cornerTop;
        float coyote, buffer, traverseTime, traverseDuration, safeTime, lastImpact;
        float wallTime, wallLock, grabLock, dashTime, swimLock, ropeLength, momentumTime;
        bool wasGrounded, airDash, swingNeedsRelease;
        TraversalAnchor anchor, candidate;
        RiverWaterVolume water;
        LineRenderer rope;
#if UNITY_EDITOR
        public bool TestInput;
        public Vector2 TestMove;
        public bool TestSprint, TestJumpPressed, TestJumpHeld, TestDrop, TestSwing, TestDash, TestCrouch, TestFire, TestFireHeld;
#endif
        void Awake()
        {
            body = GetComponent<CharacterController>();
            body.excludeLayers=1<<8;
            checkpoint = transform.position;
            Hand=GetComponent<StickyHand>();if(!Hand)Hand=gameObject.AddComponent<StickyHand>();
            var go = new GameObject("Swing tether"); go.transform.SetParent(transform, false);
            rope = go.AddComponent<LineRenderer>(); rope.positionCount = 2;
            rope.startWidth = rope.endWidth = .035f; rope.numCapVertices = 3;
            rope.sharedMaterial = ropeMaterial;
            rope.startColor = rope.endColor = Palette.Brass;
            rope.enabled = false;
        }
        void Update()
        {
            if (!ControlsEnabled || Time.deltaTime <= 0) return;
            var keys = Keyboard.current; var pad = Gamepad.current;
            Vector2 move = Vector2.zero;
            bool jump = false, held = false, sprint = false, drop = false, swing = false, dash = false, crouch=false, fire=false, fireHeld=false;
            if (keys != null)
            {
                move = new Vector2((keys.dKey.isPressed || keys.rightArrowKey.isPressed ? 1 : 0) - (keys.aKey.isPressed || keys.leftArrowKey.isPressed ? 1 : 0),
                    (keys.wKey.isPressed || keys.upArrowKey.isPressed ? 1 : 0) - (keys.sKey.isPressed || keys.downArrowKey.isPressed ? 1 : 0));
                jump = keys.spaceKey.wasPressedThisFrame; held = keys.spaceKey.isPressed;
                sprint = keys.leftShiftKey.isPressed || keys.rightShiftKey.isPressed;
                crouch = keys.leftCtrlKey.isPressed || keys.rightCtrlKey.isPressed;
                drop = keys.cKey.isPressed;fire=keys.fKey.wasPressedThisFrame;fireHeld=keys.fKey.isPressed;
                swing = keys.eKey.isPressed; dash = keys.qKey.wasPressedThisFrame;
                if (keys.rKey.wasPressedThisFrame) { Respawn(); return; }
            }
            if(Mouse.current!=null){fire|=Mouse.current.leftButton.wasPressedThisFrame;fireHeld|=Mouse.current.leftButton.isPressed;drop|=Mouse.current.rightButton.wasPressedThisFrame;}
            if (pad != null)
            {
                if (pad.leftStick.ReadValue().sqrMagnitude > move.sqrMagnitude) move = pad.leftStick.ReadValue();
                jump |= pad.buttonSouth.wasPressedThisFrame; held |= pad.buttonSouth.isPressed;
                sprint |= pad.leftStickButton.isPressed || pad.rightTrigger.ReadValue() > .4f;
                crouch |= pad.buttonEast.isPressed;drop |= pad.dpad.down.isPressed;fire |= pad.buttonNorth.wasPressedThisFrame;fireHeld |= pad.buttonNorth.isPressed; swing |= pad.leftShoulder.isPressed; dash |= pad.rightShoulder.wasPressedThisFrame;
            }
#if UNITY_EDITOR
            if (TestInput)
            {
                move = TestMove; sprint = TestSprint; jump = TestJumpPressed; held = TestJumpHeld;
                drop = TestDrop; swing = TestSwing; dash = TestDash;crouch=TestCrouch;fire=TestFire;fireHeld=TestFireHeld;
                TestJumpPressed = TestDash = TestFire = false;
            }
#endif
            manualCrouch=crouch;
            Hand.SetTrigger(fire,fireHeld);
            float remaining = Mathf.Min(Time.deltaTime, .1f);
            // Substeps keep dash and rope collisions stable during a slow frame.
            while (remaining > 0)
            {
                float dt = Mathf.Min(remaining, 1f / 60);
                Step(Vector2.ClampMagnitude(move, 1), sprint, jump, held, drop, swing, dash, dt);
                remaining -= dt; jump = dash = false;
            }
            rope.enabled = Swinging;
            if (Swinging) { rope.SetPosition(0, transform.position + Vector3.up * 1.45f); rope.SetPosition(1, anchor.transform.position); }
        }
        void Step(Vector2 input, bool sprint, bool jump, bool held, bool drop, bool swing, bool dash, float dt)
        {
            if (transform.position.y < -22 || Mathf.Abs(transform.position.x) > 420 || transform.position.z < -260 || transform.position.z > 760)
            { Respawn(); return; }
            if(drop&&Hand.Flying)Hand.Release();
            Hand.Tick(dt);crouchGrace-=dt;
            if(!Traversing&&!Hanging)SetCrouched(manualCrouch||crouchGrace>0||!RoomAt(transform.position,StandingHeight));
            wallLock -= dt; grabLock -= dt; swimLock -= dt; momentumTime -= dt;
            if (!swing) swingNeedsRelease = false;
            var yaw = Quaternion.Euler(0, heading.eulerAngles.y, 0);
            var forward = yaw * Vector3.forward;
            var direction = yaw * new Vector3(input.x, 0, input.y);
            buffer = jump ? .16f : buffer - dt;
            if (Traversing) { AdvanceTraverse(dt); return; }
            if (Leaping) { AdvanceLeap(dt); return; }
            if(Hand.Attached){vine=null;Hanging=cornering=false;anchor=null;GrappleStep(direction,jump,drop,dt);return;}
            if (Hanging) { HangStep(input, jump, drop, dt); return; }
            if(vine){VineStep(input,jump,drop,dt);return;}
            if(!drop&&grabLock<=0&&direction.sqrMagnitude>.1f&&TryCatchVine(direction)){VineStep(input,false,false,dt);return;}
            water = RiverWaterVolume.At(transform.position + Vector3.up * .65f);
            bool submerged = water && transform.position.y < water.Surface - .5f && swimLock <= 0;
            if (submerged)
            {
                if (!Swimming) { feedback?.Splash(); Log("Swim"); }
                Swimming = true; grappleFlight=false; WallRunning = false; anchor = null; dashTime = 0;
                SwimStep(direction, sprint, jump, held, drop||manualCrouch, dt); return;
            }
            Swimming = false;
            Grounded = body.isGrounded;
            if (Grounded)
            {
                grappleFlight=false;
                coyote = .13f; wallTime = 0; airDash = false;
            }
            else coyote -= dt;
            candidate = FindAnchor(forward);
            Hint = Hand.Notice.Length>0?Hand.Notice:candidate ? "Hold E · swing" : Crouched?"Crouched · release Ctrl to stand when clear":"";
            if (Swinging) { SwingStep(direction, jump, swing, dt); wasGrounded = false; return; }
            if (swing && !swingNeedsRelease && candidate && !Grounded) { BeginSwing(candidate); SwingStep(direction, false, true, dt); return; }
            if (direction.sqrMagnitude > .01f) Face(direction, dt);
            if (!drop && grabLock <= 0 && direction.sqrMagnitude > .1f)
            {
                // Free-running: sprinting into an obstacle vaults or mantles without a jump press.
                if (Grounded && (buffer > 0 || (sprint && Speed > walkSpeed * .85f)) && TryTraverse(direction.normalized, buffer <= 0)) return;
                if (!Grounded && (velocity.y < 5 || WallRunning) && TryGrab(direction.normalized)) return;
            }
            RaycastHit wall = default;
            bool foundWall = !Grounded && wallLock <= 0 && ProbeWall(direction.sqrMagnitude > .1f ? direction.normalized : visual.forward, out wall);
            if (foundWall && jump && coyote<=0 && Vector3.Dot(direction,wall.normal)>.35f)
            {
                WallNormal = wall.normal;
                var along = Vector3.ProjectOnPlane(direction, wall.normal).normalized;
                velocity = wall.normal * 4.2f + along * 3 + Vector3.up * 9;
                WallRunning = false; wallLock = .28f; wallTime = 0; grabLock = .14f; buffer = 0;
                momentumTime = .16f; airDash = false; WallJumps++; Jumps++;
                feedback?.Jump(); Log("Wall jump");
            }
            else if(foundWall && jump && Vector3.Dot(direction,wall.normal)<-.25f && wallTime<wallRunDuration)
            {
                velocity=Vector3.ProjectOnPlane(velocity,wall.normal);velocity.y=Mathf.Max(velocity.y,7.5f);
                wallTime+=.5f;buffer=0;feedback?.Climb();
            }
            else if (foundWall && sprint && direction.sqrMagnitude > .1f && wallTime < wallRunDuration && velocity.y > -18)
            {
                if (!WallRunning) { WallRuns++; feedback?.Climb(); Log("Wall run"); }
                WallRunning = true; WallNormal = wall.normal; wallTime += dt; airDash = false;
                var along = Vector3.ProjectOnPlane(direction, wall.normal); along.y = 0;
                bool upWall = along.magnitude < .35f;
                velocity = along.normalized * wallRunSpeed - wall.normal * (upWall ? .15f : 2);
                velocity.y = upWall ? (held ? 7.8f : 4.8f) : Mathf.Lerp(2.5f, -3.5f, wallTime / wallRunDuration);
                Face(upWall ? -wall.normal : along, dt);
                Action = upWall ? "Wall climb" : "Wall run";
                // Blocked overhead (a sill or balcony): stop pushing and let the ledge catch take it.
                if ((body.Move(velocity * dt) & CollisionFlags.Above) != 0) { WallRunning = false; wallTime = wallRunDuration; velocity = wall.normal * .4f; }
                wasGrounded = false; return;
            }
            else WallRunning = false;
            if (dash && !Grounded && !airDash)
            {
                dashDirection = (direction.sqrMagnitude > .1f ? direction.normalized : forward);
                dashTime = .19f; airDash = true; Dashes++; feedback?.Jump(); Log("Air dash");
            }
            if (dashTime > 0)
            {
                dashTime -= dt; velocity = dashDirection * 22 + Vector3.up * 1.8f;
                body.Move(velocity * dt); Action = "Air dash"; momentumTime = .25f; return;
            }
            if(grappleFlight&&!Grounded)SteerGrapple(direction,dt);
            else if (rollTime > 0)
            {
                rollTime -= dt;
                var carried = new Vector3(velocity.x, 0, velocity.z);
                var heading = direction.sqrMagnitude > .1f ? Vector3.RotateTowards(carried.sqrMagnitude > .01f ? carried.normalized : visual.forward, direction.normalized, 3 * dt, 0) : visual.forward;
                var rolled = heading * Mathf.Max(carried.magnitude * Mathf.Exp(-.8f * dt), walkSpeed);
                velocity.x = rolled.x; velocity.z = rolled.z;
            }
            else if (momentumTime <= 0)
            {
                var horizontal = new Vector3(velocity.x, 0, velocity.z);
                float target = Crouched ? walkSpeed * .48f : sprint ? sprintSpeed : walkSpeed;
                // In the air, speed carried from swings, zips and leaps is steered rather than braked.
                if (!Grounded && horizontal.magnitude > target && direction.sqrMagnitude > .01f && Vector3.Dot(horizontal, direction) > 0)
                    horizontal = Vector3.RotateTowards(horizontal.normalized, direction.normalized, 2.4f * dt, 0) * Mathf.MoveTowards(horizontal.magnitude, target, 2.5f * dt);
                else horizontal = Vector3.MoveTowards(horizontal, direction * target, (Grounded ? 38 : 11) * dt);
                velocity.x = horizontal.x; velocity.z = horizontal.z;
            }
            if (Grounded && velocity.y < 0) velocity.y = -2;
            if (buffer > 0 && coyote > 0)
            {
                velocity.y = Mathf.Sqrt(2 * 25 * jumpHeight); coyote = buffer = 0; Grounded = false;
                Jumps++; feedback?.Jump(); Log("Jump");
            }
            float gravity = !grappleFlight&&velocity.y > 0 && !held ? 39 : 25;
            velocity.y = Mathf.Max(-42, velocity.y - gravity * dt);
            lastImpact = velocity.y;
            var flags = body.Move(velocity * dt);
            if ((flags & CollisionFlags.Above) != 0 && velocity.y > 0) velocity.y = 0;
            Grounded = body.isGrounded;
            // Touchdown: a hard landing while moving becomes a roll that keeps the run going.
            if (Grounded && !wasGrounded)
            {
                if (lastImpact < -15 && Speed > 3.5f && !Crouched) { rollTime = RollDuration; feedback?.Land(.22f); Log("Roll"); }
                else if (lastImpact < -3) feedback?.Land(Mathf.Clamp01(-lastImpact / 18));
            }
            Action = rollTime > 0 ? "Roll" : Grounded && Crouched ? "Crouch" : Grounded ? (Speed > .2f ? (sprint ? "Sprint" : "Wander") : "Explore") : (momentumTime > 0 ? "Leap" : "Airborne");
            if (Grounded && Physics.Raycast(transform.position + Vector3.up * .2f, Vector3.down, out var floor, .65f, worldMask)
                && floor.normal.y > .95f)
            {
                safeTime += dt; if (safeTime > .25f) checkpoint = transform.position;
            }
            else safeTime = 0;
            wasGrounded = Grounded;
        }
        void Face(Vector3 direction, float dt)
        {
            direction.y = 0;
            if (direction.sqrMagnitude > .01f) visual.rotation = Quaternion.Slerp(visual.rotation, Quaternion.LookRotation(direction), 1 - Mathf.Exp(-16 * dt));
        }
        bool ProbeWall(Vector3 direction, out RaycastHit best)
        {
            Vector3 origin = transform.position + Vector3.up * 1.05f;
            best = default; float distance = 1.05f;
            for (int i = 0; i < 4; i++)
            {
                Vector3 ray = Quaternion.Euler(0,i==0?0:i==1?90:i==2?-90:180,0)*direction;
                if (Physics.SphereCast(origin, .18f, ray, out var hit, distance, worldMask, QueryTriggerInteraction.Ignore) && Mathf.Abs(hit.normal.y) < .22f)
                { best = hit; distance = hit.distance; }
            }
            return best.collider != null;
        }
        bool TryTraverse(Vector3 direction, bool auto = false)
        {
            Vector3 foot = transform.position;
            if (!Physics.SphereCast(foot + Vector3.up * .6f, .15f, direction, out var wall, auto ? .7f : .85f, worldMask, QueryTriggerInteraction.Ignore) || Mathf.Abs(wall.normal.y) > .25f) return false;
            Vector3 over = wall.point - wall.normal * .5f;
            if (!Physics.Raycast(new Vector3(over.x, foot.y + 2.2f, over.z), Vector3.down, out var top, 2.7f, worldMask, QueryTriggerInteraction.Ignore)) return false;
            float height = top.point.y - foot.y;
            if (height < .35f || height > 2.05f || !Walkable(top.normal)) return false;
            // Low and thin: vault clean over to level ground beyond, keeping pace.
            if (height < 1.2f)
                foreach (float depth in new[] { .95f, 1.35f, 1.8f })
                {
                    Vector3 beyond = wall.point - wall.normal * depth;
                    if (!Physics.Raycast(new Vector3(beyond.x, top.point.y + .5f, beyond.z), Vector3.down, out var floor, height + 1.3f, worldMask, QueryTriggerInteraction.Ignore)) continue;
                    if (!Walkable(floor.normal) || Mathf.Abs(floor.point.y - foot.y) > .6f || floor.point.y > top.point.y - .3f) continue;
                    if (BeginTraverse(LandingFoot(floor), "Speed vault", default, top.point.y)) return true;
                }
            if (auto)
            {
                // Only run up onto tops wide enough to keep running on.
                Vector3 deeper = wall.point - wall.normal * 1.1f;
                if (!Physics.Raycast(new Vector3(deeper.x, top.point.y + .5f, deeper.z), Vector3.down, out var far, 1, worldMask, QueryTriggerInteraction.Ignore) || Mathf.Abs(far.point.y - top.point.y) > .3f) return false;
            }
            return BeginTraverse(LandingFoot(top), height < 1.1f ? "Vault" : "Mantle");
        }
        bool TryGrab(Vector3 direction,bool releaseHand=true)
        {
            Vector3 foot=transform.position;
            for(int angle=0;angle<3;angle++)for(int level=0;level<3;level++)
            {
                Vector3 ray=Quaternion.Euler(0,angle==0?0:angle==1?25:-25,0)*direction;
                float probeHeight=level==0?1.5f:level==1?1.1f:1.85f;
                if(!Physics.SphereCast(foot+Vector3.up*probeHeight,.12f,ray,out var wall,.9f,worldMask,QueryTriggerInteraction.Ignore)||Mathf.Abs(wall.normal.y)>.25f)continue;
                if(!ProbeLedgeTop(wall,foot.y+2.45f,1.65f,out var top))continue;
                float height=top.point.y-foot.y;if(height<.85f||height>2.4f)continue;
                Vector3 normal=Vector3.ProjectOnPlane(wall.normal,Vector3.up).normalized;
                var hang=wall.point+normal*.38f;hang.y=top.point.y-1.65f;
                if(!RoomAt(hang,StandingHeight)||!ClearSweep(foot,hang,body.height))continue;
                body.Move(hang-foot);if(Vector3.Distance(transform.position,hang)>.22f)continue;
                SetCrouched(false);ledgeTop=top.point;ledgeNormal=normal;ledgeWallPoint=wall.point;
                float caught=WallRunning||Speed>walkSpeed?.2f:0;
                Hanging=true;grappleFlight=false;Grounded=WallRunning=cornering=false;velocity=Vector3.zero;buffer=0;
                hangTime=caught;climbRetry=0;climbBlocked=false;if(releaseHand)Hand.Release();
                airDash=false;Grabs++;feedback?.Climb();Face(-ledgeNormal,1);
                Action="Ledge hang";Hint="W / Space pull up · A / D around corners · C drop";Log(Action);return true;
            }
            return false;
        }
        bool ProbeLedgeTop(RaycastHit wall,float y,float distance,out RaycastHit top)
        {
            foreach(float inset in LedgeInsets)
            {
                Vector3 over=wall.point-wall.normal*inset;
                if(Physics.Raycast(new Vector3(over.x,y,over.z),Vector3.down,out top,distance,worldMask,QueryTriggerInteraction.Ignore)&&Walkable(top.normal))return true;
            }
            top=default;return false;
        }
        void HangStep(Vector2 input, bool jump, bool drop, float dt)
        {
            hangTime+=dt;climbRetry-=dt;
            Action="Ledge hang";Hint=climbBlocked?"A / D find clearance · F sticky hand · C drop":"W / Space pull up · A / D around corners · S + Space hop off · C drop";
            if(drop){Hanging=cornering=false;grabLock=.6f;velocity=ledgeNormal*.8f+Vector3.down;return;}
            if(cornering)
            {
                cornerClimbRequested|=jump||input.y>.5f;
                AdvanceCorner(cornerClimbRequested?cornerSign:input.x,dt);
                if(!cornering&&cornerClimbRequested)
                {
                    cornerClimbRequested=false;
                    if(TryClimbFromHang())Hanging=false;else climbBlocked=true;
                }
                return;
            }
            if(jump&&Mathf.Abs(input.x)>.5f&&TryLeap(Mathf.Sign(input.x)))return;
            if(jump||(input.y>.5f&&hangTime>.25f&&climbRetry<=0))
            {
                if(jump&&input.y<-.35f)
                {
                    Hanging=false;grabLock=.5f;wallLock=.25f;momentumTime=.15f;
                    velocity=ledgeNormal*3.2f+Vector3.up*7;Jumps++;feedback?.Jump();return;
                }
                if(TryClimbFromHang()){Hanging=false;return;}
                // Wall continues above: leap to the next handhold instead.
                if((jump||hangTime>.35f)&&TryLeap(0))return;
                climbBlocked=true;climbRetry=.2f;
            }
            if(Mathf.Abs(input.x)<.15f)return;
            Vector3 tangent=Vector3.Cross(Vector3.up,-ledgeNormal).normalized;
            Vector3 next=transform.position+tangent*(input.x*2.1f*dt);
            Vector3 probe=new Vector3(next.x,ledgeTop.y-.07f,next.z);
            if(Physics.Raycast(probe,-ledgeNormal,out var wall,.75f,worldMask,QueryTriggerInteraction.Ignore)&&Mathf.Abs(wall.normal.y)<.3f)
            {
                if(!ProbeLedgeTop(wall,ledgeTop.y+.18f,.45f,out var top))return;
                next=wall.point+wall.normal*.38f;next.y=top.point.y-1.65f;
                if(!RoomAt(next,StandingHeight)||!ClearSweep(transform.position,next,StandingHeight))return;
                body.Move(next-transform.position);ledgeTop=top.point;ledgeNormal=wall.normal;ledgeWallPoint=wall.point;
                Action="Shimmy";climbBlocked=false;Face(-ledgeNormal,dt);return;
            }
            Vector3 travel=tangent*Mathf.Sign(input.x);
            Vector3 around=probe+travel*.65f-ledgeNormal*.65f;
            if(!Physics.Raycast(around,-travel,out wall,1.15f,worldMask,QueryTriggerInteraction.Ignore)||Vector3.Dot(wall.normal,travel)<.65f)return;
            if(!ProbeLedgeTop(wall,ledgeTop.y+.18f,.45f,out var sideTop))return;
            float determinant=ledgeNormal.x*wall.normal.z-ledgeNormal.z*wall.normal.x;
            if(Mathf.Abs(determinant)<.5f)return;
            float oldPlane=Vector3.Dot(ledgeNormal,ledgeWallPoint),newPlane=Vector3.Dot(wall.normal,wall.point);
            cornerCenter=new Vector3((oldPlane*wall.normal.z-ledgeNormal.z*newPlane)/determinant,sideTop.point.y-1.65f,(ledgeNormal.x*newPlane-oldPlane*wall.normal.x)/determinant);
            cornerFrom=transform.position-cornerCenter;cornerFrom.y=0;
            if(cornerFrom.magnitude>.65f)return;
            cornerTo=wall.normal*.4f;cornerTop=sideTop.point;cornerTime=0;cornerSign=Mathf.Sign(input.x);cornering=true;cornerClimbRequested=false;
            AdvanceCorner(input.x,dt);
        }
        void AdvanceCorner(float input,float dt)
        {
            if(Mathf.Abs(input)<.15f)return;
            float nextTime=Mathf.Clamp01(cornerTime+input*cornerSign*dt/.32f);
            Vector3 radial=Vector3.Slerp(cornerFrom,cornerTo,nextTime);
            Vector3 next=cornerCenter+radial;
            if(!RoomAt(next,StandingHeight)||!ClearSweep(transform.position,next,StandingHeight))return;
            body.Move(next-transform.position);cornerTime=nextTime;ledgeNormal=radial.normalized;
            Action="Corner shimmy";Face(-ledgeNormal,dt);
            if(cornerTime>=1||cornerTime<=0)
            {
                cornering=false;ledgeNormal=(cornerTime>=1?cornerTo:cornerFrom).normalized;
                ledgeWallPoint=cornerCenter;ledgeTop=cornerTop;climbBlocked=false;
            }
        }
        bool TryClimbFromHang()
        {
            Vector3 edge=transform.position-ledgeNormal*.38f;edge.y=ledgeTop.y;
            foreach(float side in LandingSides)foreach(float depth in LandingDepths)for(int ray=0;ray<2;ray++)
            {
                Vector3 over=edge-ledgeNormal*depth+Vector3.Cross(Vector3.up,ledgeNormal)*side;
                float rise=ray==0?.32f:1.3f;
                if(!Physics.Raycast(over+Vector3.up*rise,Vector3.down,out var top,rise+1.3f,worldMask,QueryTriggerInteraction.Ignore))continue;
                float delta=top.point.y-ledgeTop.y;
                if(!Walkable(top.normal)||delta>1.2f||delta< -1.25f)continue;
                foreach(float retreat in Retreats)
                    if(BeginTraverse(LandingFoot(top),"Climb up",ledgeNormal*retreat))return true;
            }
            return false;
        }
        bool BeginTraverse(Vector3 end,string action,Vector3 retreat=default,float over=float.NegativeInfinity)
        {
            Vector3 foot=transform.position;bool fromHang=Hanging;
            Vector3 liftStart=foot+retreat;
            for(int stance=0;stance<2;stance++)foreach(float clearance in MantleLifts)
            {
                float liftY=Mathf.Max(Mathf.Max(end.y,over),fromHang?ledgeTop.y:end.y)+clearance;
                Vector3 high=new Vector3(liftStart.x,liftY,liftStart.z),across=new Vector3(end.x,liftY,end.z);
                float height=stance==0&&!manualCrouch?StandingHeight:CrouchHeight;
                if(!RoomAt(end,height)||!ClearSweep(foot,liftStart,height)||!ClearSweep(liftStart,high,height)||!ClearSweep(high,across,height)||!ClearSweep(across,end,height))continue;
                SetCrouched(height<StandingHeight);crouchGrace=Crouched?.3f:0;
                traverseStart=foot;traverseEnd=end;traverseLiftStart=liftStart;traverseLiftY=liftY;traverseFromHang=fromHang;traverseTime=0;
                var flat=new Vector3(end.x-foot.x,0,end.z-foot.z);
                carrySpeed=fromHang?0:Speed;carryDir=flat.sqrMagnitude>.001f?flat.normalized:visual.forward;
                float pace=Mathf.Clamp01(carrySpeed/sprintSpeed);
                traverseDuration=action.EndsWith("ault")?Mathf.Lerp(.32f,.24f,pace):fromHang?.62f:Mathf.Lerp(.45f,.36f,pace);
                Traversing=true;grappleFlight=Hand.Attached;cornering=false;velocity=Vector3.zero;buffer=coyote=0;
                Action=Crouched?"Crouched pull-up":action;Hint="";Traversals++;feedback?.Climb();Log(Action);return true;
            }
            return false;
        }
        // Climb leap: hang to hang along a facade, up (side 0) or sideways (side ±1).
        bool TryLeap(float side)
        {
            // Scan just outside the facade plane for the next handhold: sills, string courses, cornices, balconies.
            Vector3 foot=transform.position,tangent=Vector3.Cross(Vector3.up,-ledgeNormal).normalized;
            Vector3 plane=ledgeWallPoint;plane.y=ledgeTop.y;
            float along=Vector3.Dot(foot-plane,tangent);
            var lateral=side==0?new[]{0f,.45f,-.45f}:new[]{1.2f*side,1.7f*side,2.2f*side,2.7f*side};
            float from=side==0?.7f:-1.1f,to=side==0?4.4f:1.1f;
            foreach(float s in lateral)
            {
                // The facade plane is the deepest wall hit above the hands; the held ledge itself sticks out of it.
                float depth=float.NegativeInfinity;
                foreach(float probe in new[]{.6f,1.3f,2.1f})
                {
                    Vector3 start=plane+tangent*(along+s)+ledgeNormal*1.2f;start.y=ledgeTop.y+probe;
                    if(Physics.Raycast(start,-ledgeNormal,out var face,2,worldMask,QueryTriggerInteraction.Ignore)&&Mathf.Abs(face.normal.y)<.3f)depth=Mathf.Max(depth,face.distance-1.2f);
                }
                if(float.IsNegativeInfinity(depth))continue;
                Vector3 facade=plane+tangent*(along+s)-ledgeNormal*depth;
            for(float h=from;h<=to;h+=.2f)foreach(float outward in new[]{.08f,.17f})
            {
                Vector3 column=facade+ledgeNormal*outward;column.y=ledgeTop.y+h+.3f;
                if(!Physics.Raycast(column,Vector3.down,out var top,.45f,worldMask,QueryTriggerInteraction.Ignore)||!Walkable(top.normal))continue;
                if(side==0&&top.point.y<ledgeTop.y+.55f)continue;
                var hang=new Vector3(facade.x,top.point.y-1.65f,facade.z)+ledgeNormal*Mathf.Max(.38f,outward+.36f);
                if(!RoomAt(hang))continue;
                bool clear=false;
                foreach(float arc in new[]{.35f,.8f,1.2f})
                {
                    var outwardStart=foot+ledgeNormal*arc;var outwardEnd=hang+ledgeNormal*arc;
                    if(ClearSweep(foot,outwardStart)&&ClearSweep(outwardStart,outwardEnd)&&ClearSweep(outwardEnd,hang)){clear=true;leapArc=arc;break;}
                }
                if(!clear)continue;
                leapStart=foot;leapEnd=hang;leapTop=top.point;leapNormal=ledgeNormal;leapWall=facade;leapTime=0;
                leapDuration=.26f+Vector3.Distance(foot,hang)*.05f;
                Leaping=true;Hanging=cornering=false;climbBlocked=false;velocity=Vector3.zero;
                Action="Climb leap";Hint="";Traversals++;feedback?.Jump();Log(Action);return true;
            }
            }
            return false;
        }
        void AdvanceLeap(float dt)
        {
            leapTime+=dt;float t=Mathf.Clamp01(leapTime/leapDuration),e=t*t*(3-2*t);
            var desired=Vector3.Lerp(leapStart,leapEnd,e)+(leapNormal+ledgeNormal)*.5f*(leapArc*Mathf.Sin(t*Mathf.PI));
            desired.y=Mathf.Lerp(leapStart.y,leapEnd.y,1-(1-t)*(1-t))+Mathf.Sin(t*Mathf.PI)*.25f;
            body.Move(desired-transform.position);Face(-Vector3.Lerp(ledgeNormal,leapNormal,e),dt);
            if(t<1)return;
            Leaping=false;Hanging=true;ledgeTop=leapTop;ledgeNormal=leapNormal;ledgeWallPoint=leapWall;
            hangTime=.1f;climbRetry=0;Grabs++;feedback?.Climb();Action="Ledge hang";
        }
        Vector3 LandingFoot(RaycastHit hit)=>hit.point+Vector3.up*(.04f+.29f*(1/Mathf.Max(.6f,hit.normal.y)-1));
        bool Walkable(Vector3 normal)=>normal.y>=Mathf.Cos(body.slopeLimit*Mathf.Deg2Rad);
        void SetCrouched(bool value)
        {
            if(!value&&!RoomAt(transform.position,StandingHeight))value=true;
            if(Crouched==value)return;
            Crouched=value;body.height=value?CrouchHeight:StandingHeight;body.center=Vector3.up*(body.height*.5f);
        }
        bool RoomAt(Vector3 foot,float height=StandingHeight)=>!Physics.CheckCapsule(foot+Vector3.up*.33f,foot+Vector3.up*(height-.29f),.28f,worldMask,QueryTriggerInteraction.Ignore);
        bool ClearSweep(Vector3 start,Vector3 end,float height=StandingHeight)
        {
            if(!RoomAt(start,height)||!RoomAt(end,height))return false;
            Vector3 delta=end-start;
            return delta.sqrMagnitude<.0001f||!Physics.CapsuleCast(start+Vector3.up*.33f,start+Vector3.up*(height-.29f),.27f,delta.normalized,delta.magnitude,worldMask,QueryTriggerInteraction.Ignore);
        }
        void AdvanceTraverse(float dt)
        {
            traverseTime += dt; float t = Mathf.Clamp01(traverseTime / traverseDuration);
            float retreat=Mathf.SmoothStep(0,1,Mathf.Clamp01(t/.15f));
            float lift=Mathf.SmoothStep(0,1,Mathf.Clamp01((t-(traverseFromHang?.16f:0))/(traverseFromHang?.36f:.42f)));
            float across=Mathf.SmoothStep(0,1,Mathf.Clamp01((t-.52f)/.33f));
            float settle=Mathf.SmoothStep(0,1,Mathf.Clamp01((t-.85f)/.15f));
            Vector3 desired=Vector3.Lerp(traverseStart,traverseLiftStart,retreat);
            desired=Vector3.Lerp(desired,new Vector3(traverseEnd.x,desired.y,traverseEnd.z),across);
            desired.y=Mathf.Lerp(Mathf.Lerp(traverseStart.y,traverseLiftY,lift),traverseEnd.y,settle);
            body.Move(desired - transform.position);
            if (t >= 1) { Traversing = false; velocity = carrySpeed > walkSpeed * .6f ? carryDir * carrySpeed * .95f : Vector3.zero; velocity.y = -2;crouchGrace=Crouched?.3f:0;
                if(Vector3.Distance(transform.position,traverseEnd)<.25f)checkpoint=transform.position;feedback?.Land(.2f); }
        }
        bool TryCatchVine(Vector3 direction)
        {
            Vector3 foot=transform.position;ClimbableVine closest=null;float distance=1.25f;
            foreach(var plant in ClimbableVine.Active)
            {
                if(Vector3.Dot(direction,plant.Outward)>-.25f||foot.y+1.5f<plant.Bottom.y-.25f||foot.y+1.2f>plant.Top.y+.4f)continue;
                Vector3 center=plant.Top+plant.Outward*.5f;center.y=foot.y;
                float gap=Vector3.Distance(foot,center);
                if(gap>=distance||!RoomAt(center,body.height)||!ClearSweep(foot,center,body.height))continue;
                distance=gap;closest=plant;
            }
            if(!closest)return false;
            vine=closest;grappleFlight=false;Hand.Release();anchor=null;velocity=Vector3.zero;
            Grounded=Swimming=WallRunning=false;buffer=0;airDash=false;
            Face(-vine.Outward,1);feedback?.Climb();return true;
        }
        void VineStep(Vector2 input,bool jump,bool drop,float dt)
        {
            Vector3 normal=vine.Outward;
            Action="Vine climb";Hint="W / S climb vine · Space pull up · S + Space hop off · C drop";
            if(drop||(jump&&input.y<-.35f))
            {
                vine=null;grabLock=.6f;velocity=jump?normal*3+Vector3.up*7:Vector3.down;
                momentumTime=jump?.15f:0;return;
            }
            if((input.y>.1f||jump)&&transform.position.y>=vine.Top.y-2.6f)
            {
                ledgeTop=vine.Top+Vector3.up*.35f;ledgeNormal=normal;Hanging=true;
                bool climbed=TryClimbFromHang();Hanging=false;
                if(climbed){vine=null;return;}
            }
            Vector3 sideways=Vector3.Cross(Vector3.up,-normal);
            Vector3 center=vine.Top+normal*.5f;
            float lateral=Mathf.Clamp(Vector3.Dot(transform.position-center,sideways)+input.x*1.8f*dt,-vine.width*.5f,vine.width*.5f);
            Vector3 next=center+sideways*lateral;
            next.y=Mathf.Clamp(transform.position.y+input.y*3*dt,vine.Bottom.y-1.35f,vine.Top.y-.9f);
            Vector3 before=transform.position;
            var flags=body.Move(Vector3.ClampMagnitude(new Vector3(next.x-before.x,0,next.z-before.z),3*dt)+Vector3.up*(next.y-before.y));
            if((flags&CollisionFlags.Above)!=0){SetCrouched(true);crouchGrace=.4f;}
            velocity=(transform.position-before)/dt;Grounded=false;Face(-normal,dt);
            if(input.y<-.1f&&(body.isGrounded||next.y<=vine.Bottom.y-1.34f)){vine=null;grabLock=.4f;velocity=Vector3.down;}
        }
        public void BeginGrapple()
        {
            grappleFlight=true;WallRunning=Swimming=false;dashTime=buffer=coyote=0;
            airDash=false;Swings++;
        }
        public void EndGrapple()
        {
            Zipping=false;
            grabLock=.35f;wallLock=.25f;buffer=coyote=0;
        }
        void SteerGrapple(Vector3 direction,float dt)
        {
            var horizontal=new Vector3(velocity.x,0,velocity.z);
            float cap=Mathf.Max(walkSpeed,horizontal.magnitude);
            horizontal+=direction*(walkSpeed*18*.12f*dt);
            horizontal=Vector3.ClampMagnitude(horizontal,cap);
            velocity.x=horizontal.x;velocity.z=horizontal.z;
        }
        void GrappleStep(Vector3 direction,bool jump,bool drop,float dt)
        {
            Grounded=body.isGrounded;wasGrounded=Grounded;
            Action=Zipping?"Zip":"Grapple swing";Hint="Hold click / F · WASD steer · Space zip to the hand · Release to let go";
            if(drop){Hand.Release();Zipping=false;return;}
            if(jump&&Grounded&&Hand.Point.y<=transform.position.y+1.5f){velocity.y=Mathf.Sqrt(50*jumpHeight);Jumps++;feedback?.Jump();}
            if(jump&&!Zipping&&(!Grounded||Hand.Point.y>transform.position.y+1.5f)){Zipping=true;feedback?.Jump();Log("Zip");}
            if(Zipping){ZipStep(direction,dt);return;}
            if(Grounded&&direction.sqrMagnitude<.01f)
            {velocity.x*=Mathf.Exp(-8*dt);velocity.z*=Mathf.Exp(-8*dt);}
            SteerGrapple(direction,dt);
            Hand.Pull(ref velocity,dt);
            velocity.y-=25*dt;
            body.Move(velocity*dt);
            Vector3 radial=TetherOrigin-Hand.Point;
            if(radial.magnitude>Hand.maxReach)
            {
                var outward=radial.normalized;
                body.Move(-outward*(radial.magnitude-Hand.maxReach));
                float away=Vector3.Dot(velocity,outward);
                if(away>0)velocity-=outward*away;
                // A blocked correction keeps the grip; collision takes priority over rope length.
            }
            Grounded=body.isGrounded;lastImpact=velocity.y;
            if(Grounded&&velocity.y<0)velocity.y=-2;
            if(direction.sqrMagnitude>.05f)Face(direction,dt);else Face(velocity,dt);
            if(Hand.Attached&&direction.sqrMagnitude>.1f&&velocity.y>-.5f&&Speed<8)
            {
                Vector3 normal=Vector3.ProjectOnPlane(Hand.Normal,Vector3.up).normalized;
                if(normal.sqrMagnitude>.5f&&Vector3.Dot(direction,normal)<-.25f)
                {
                    var savedPosition=transform.position;var savedVelocity=velocity;
                    if(TryGrab(-normal,false))
                    {
                        if(TryClimbFromHang()){Hanging=false;return;}
                        Hanging=false;grappleFlight=true;body.Move(savedPosition-transform.position);velocity=savedVelocity;
                    }
                }
            }
        }
        // Reel in hard toward the hand, then catch the ledge it stuck to (or fly on past it).
        void ZipStep(Vector3 direction,float dt)
        {
            Vector3 toward=Hand.Point-TetherOrigin;
            velocity=Vector3.Lerp(velocity,toward.normalized*21+Vector3.up*1.5f,1-Mathf.Exp(-9*dt));
            var flags=body.Move(velocity*dt);
            Face(new Vector3(toward.x,0,toward.z),dt);
            if(toward.magnitude>1.9f&&(flags&CollisionFlags.Sides)==0)return;
            var normal=Vector3.ProjectOnPlane(Hand.Normal,Vector3.up);
            Zipping=false;Hand.Release();momentumTime=.12f;
            if(normal.sqrMagnitude>.25f)
            {
                normal.Normalize();
                if(TryGrab(-normal,false)){if(TryClimbFromHang())Hanging=false;return;}
            }
            velocity.y=Mathf.Max(velocity.y,6.5f);
        }
        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if(!grappleFlight)return;
            float inward=Vector3.Dot(velocity,hit.normal);
            if(inward<0)velocity-=hit.normal*inward;
        }
        void SwimStep(Vector3 direction, bool sprint, bool jump, bool held, bool dive, float dt)
        {
            Grounded = false; airDash = false; wallTime = 0;
            Hint = "Space rise / leap · Ctrl dive · Shift fast swim";
            if (direction.sqrMagnitude > .1f)
            {
                Face(direction, dt);
                if (jump && TryTraverse(direction.normalized)) { swimLock = .45f; Swimming = false; return; }
            }
            var target = direction * (sprint ? swimSpeed * 1.45f : swimSpeed);
            float depth = water.Surface - transform.position.y;
            target.y = dive ? -4.5f : held ? 4.5f : Mathf.Clamp((depth - 1.0f) * 4, -2.5f, 3.5f);
            velocity = Vector3.Lerp(velocity, target, 1 - Mathf.Exp(-5 * dt));
            if (jump && depth < 1.35f)
            {
                velocity.y = 10; swimLock = .55f; Swimming = false; Jumps++; feedback?.Splash();
            }
            body.Move(velocity * dt); Action = dive ? "Dive" : "Swim"; wasGrounded = false;
        }
        TraversalAnchor FindAnchor(Vector3 forward)
        {
            TraversalAnchor best = null; float score = float.MaxValue;
            Vector3 hand = transform.position + Vector3.up * 1.4f;
            foreach (var item in TraversalAnchor.Active)
            {
                var delta = item.transform.position - hand; float length = delta.magnitude;
                if (length > item.reach || length < 2 || delta.y < .5f || Vector3.Dot(forward, delta.normalized) < -.1f) continue;
                if (Physics.Raycast(hand, delta.normalized, length - .3f, worldMask, QueryTriggerInteraction.Ignore)) continue;
                float value = length - Vector3.Dot(forward, delta.normalized) * 7;
                if (value < score) { best = item; score = value; }
            }
            return best;
        }
        void BeginSwing(TraversalAnchor target)
        {
            anchor = target; ropeLength = Vector3.Distance(transform.position + Vector3.up * 1.4f, anchor.transform.position);
            velocity += visual.forward * 2; WallRunning = false; Grabs++; Swings++; airDash = false;
            feedback?.Climb(); Log("Swing");
        }
        void SwingStep(Vector3 direction, bool jump, bool hold, float dt)
        {
            Hint = "WASD pump · release E / Space launch"; Action = "Swing"; Grounded = false;
            if (!hold || jump)
            {
                anchor = null; swingNeedsRelease = true;
                velocity.y = Mathf.Max(0, velocity.y) + (jump ? 8.5f : 1.5f); momentumTime = .65f; grabLock = .12f;
                feedback?.Jump(); return;
            }
            var pivot = anchor.transform.position;
            var hand = transform.position + Vector3.up * 1.4f;
            var radial = (hand - pivot).normalized;
            velocity += (Vector3.down * 22 + Vector3.ProjectOnPlane(direction, radial) * 21) * dt;
            Vector3 next = hand + velocity * dt;
            if (Vector3.Distance(next, pivot) > ropeLength)
            {
                next = pivot + (next - pivot).normalized * ropeLength;
                velocity = Vector3.ProjectOnPlane(velocity, (next - pivot).normalized);
            }
            var flags = body.Move(next - hand);
            Face(velocity, dt);
            if ((flags & CollisionFlags.Sides) != 0 && Speed > 5) { anchor = null; velocity *= .4f; }
        }
        public void Respawn()
        {
            Teleport(checkpoint); Respawns++; feedback?.Land(.45f); Log("Recovered at safe landing");
        }
        public void SetCheckpoint(Vector3 point) => checkpoint = point;
        public void Teleport(Vector3 position)
        {
            body.enabled = false; transform.position = position; body.enabled = true;
            body.Move(Vector3.zero); // refresh contact flags so a mid-air teleport is not grounded
            velocity = Vector3.zero; Traversing = Hanging = WallRunning = Swimming = cornering = false; anchor = null;
            vine=null;grappleFlight=false;Leaping=Zipping=false;rollTime=0;Hand.ResetHand();crouchGrace=0;SetCrouched(manualCrouch||!RoomAt(position,StandingHeight));
            buffer = coyote = wallTime = dashTime = 0; wallLock = grabLock = swimLock = 0;
            momentumTime = safeTime = 0; wasGrounded = Grounded = false; airDash = swingNeedsRelease = false;
            if (rope) rope.enabled = false;
            Physics.SyncTransforms();
        }
        static void Log(string message)
        {
if (Application.isEditor || Debug.isDebugBuild) Debug.Log("[Traversal] " + message);
        }
    }
}
