using UnityEngine;

namespace TinyWorld
{
    // Procedural animation for the jointed wanderer (Assets/Models/Wanderer.fbx).
    // Joints rest at identity: +X right, +Z forward. Negative X swings a limb forward.
    public sealed class WandererPose : MonoBehaviour
    {
        public WandererMotor player;
        public Transform rig, hips, chest, head, capeUpper, capeLower, scarfTail;
        public Transform upperArmL, forearmL, handL, upperArmR, forearmR, handR;
        public Transform thighL, shinL, footL, thighR, shinR, footR;
        public Transform eyeL, eyeR;
        [Header("Tuning")]
        public float strideWalk = 1.25f, strideRun = 2.1f, legSwing = 34, runSwing = 52, armSwing = 26, lean = 11, bank = 10;

        const float Thigh = .4f, Shin = .41f, Ankle = .09f, HipHeight = .9f;
        float phase, speed, impact, stretch, blink, blinkTimer = 2, lastYaw, yawRate, idleTime;
        Vector3 lastVelocity, accel, hipsRest;
        Spring capePitch, capeRoll, capeLowerPitch, scarfPitch, scarfRoll, leanSpring, bankSpring, crouchSpring, airSpring;
        Quaternion headLook = Quaternion.identity;
        Quaternion[] blended;
        [Tooltip("How fast joints blend toward each new pose; lower is softer.")] public float poseBlend = 20;

        struct Spring
        {
            public float x, v;
            public float Step(float target, float frequency, float damping, float dt)
            {
                float w = frequency * 2 * Mathf.PI;
                v += (w * w * (target - x) - 2 * damping * w * v) * dt; x += v * dt; return x;
            }
        }

        void Awake() { hipsRest = hips.localPosition; lastYaw = transform.eulerAngles.y; }
        public void Impact(float strength) { impact = Mathf.Max(impact, strength); stretch = Mathf.Min(stretch, -.1f * Mathf.Clamp01(strength * 1.5f)); }
        public void Launch() => stretch = .1f;

        static Quaternion E(float x, float y = 0, float z = 0) => Quaternion.Euler(x, y, z);

        void LateUpdate()
        {
            float dt = Mathf.Min(Time.deltaTime, .05f);
            if (dt <= 0) return;
            var v = player.Velocity;
            var local = transform.InverseTransformDirection(v);
            accel = Vector3.Lerp(accel, (v - lastVelocity) / dt, 1 - Mathf.Exp(-10 * dt)); lastVelocity = v;
            var localAccel = transform.InverseTransformDirection(accel);
            float yaw = transform.eulerAngles.y;
            yawRate = Mathf.Lerp(yawRate, Mathf.DeltaAngle(lastYaw, yaw) / dt, 1 - Mathf.Exp(-12 * dt)); lastYaw = yaw;
            speed = Mathf.Lerp(speed, player.Speed, 1 - Mathf.Exp(-14 * dt));
            float run = Mathf.InverseLerp(player.walkSpeed * .9f, player.sprintSpeed, speed);
            float moving = Mathf.Clamp01(speed / 1.2f);
            impact = Mathf.MoveTowards(impact, 0, dt * 3.2f);
            stretch = Mathf.Lerp(stretch, 0, 1 - Mathf.Exp(-9 * dt));

            // ---- locomotion gait (grounded default) ----
            float stride = Mathf.Lerp(strideWalk, strideRun, run);
            phase += speed * dt / stride * Mathf.PI;
            float s = Mathf.Sin(phase), c = Mathf.Cos(phase);
            float swing = Mathf.Lerp(legSwing, runSwing, run) * moving;
            float legL = -swing * s, legR = swing * s;
            if (legL > 0) legL *= .55f; if (legR > 0) legR *= .55f;  // legs reach further forward than back
            float kneeBase = 6 + run * 10;
            float kneeL = kneeBase + Mathf.Lerp(28, 75, run) * moving * Mathf.Pow(Mathf.Max(0, c), 1.4f) + 12 * moving * Mathf.Max(0, -s);
            float kneeR = kneeBase + Mathf.Lerp(28, 75, run) * moving * Mathf.Pow(Mathf.Max(0, -c), 1.4f) + 12 * moving * Mathf.Max(0, s);
            float armL = armSwing * (1 + run * .9f) * moving * s, armR = -armL;
            float elbow = Mathf.Lerp(14, 78, run) * moving + 8;
            float abduct = 5 + run * 4;
            float hipTwist = 7 * s * moving, hipRoll = 3 * s * moving * (1 - run * .4f);

            float breath = Mathf.Sin(Time.time * 2.2f);
            idleTime = moving > .2f ? 0 : idleTime + dt;
            float idleSway = Mathf.Sin(Time.time * .7f) * (1 - moving);
            float leanTarget = Mathf.Clamp(speed * 1.1f + localAccel.z * .55f, -10, 22) * (player.Grounded ? 1 : .4f) + lean * run * .5f;
            float leanAngle = leanSpring.Step(leanTarget, 2.2f, .7f, dt);
            float bankAngle = bankSpring.Step(Mathf.Clamp(-yawRate * .045f * Mathf.Clamp01(speed / 4), -bank, bank), 2.5f, .75f, dt);

            Quaternion hipsRot = E(0, hipTwist, hipRoll + idleSway * 1.5f + bankAngle * .5f);
            Quaternion chestRot = E(leanAngle + breath * .8f, -hipTwist * 1.6f, bankAngle * .6f);
            Quaternion uaL = E(armL, 0, -abduct), uaR = E(armR, 0, abduct), faL = E(-elbow), faR = E(-elbow);
            Quaternion thL = E(legL, 0, -1.5f), thR = E(legR, 0, 1.5f), shL = E(kneeL), shR = E(kneeR);
            Quaternion ftL = E(-(legL + kneeL) * .55f), ftR = E(-(legR + kneeR) * .55f);
            Quaternion headRot = E(-leanAngle * .55f + breath * .5f, 0, -bankAngle * .3f);
            float crouch = crouchSpring.Step(player.Crouched ? 1 : 0, 3.5f, .8f, dt);
            bool grounded = player.Grounded;
            float air = airSpring.Step(grounded || player.Hanging || player.Swimming || player.VineClimbing ? 0 : 1, 4, .8f, dt);
            float hipDrop = 0; bool plantFeet = true;

            // ---- air ----
            if (air > .01f && !player.Swimming)
            {
                float vy = player.VerticalSpeed;
                float rise = Mathf.Clamp01(vy / 8), fall = Mathf.Clamp01(-vy / 14);
                Quaternion aThL = E(Mathf.Lerp(-38, -62, rise) + fall * 28, 0, -4), aThR = E(Mathf.Lerp(12, 2, rise) - fall * 8, 0, 4);
                Quaternion aShL = E(Mathf.Lerp(75, 100, rise) - fall * 45), aShR = E(Mathf.Lerp(65, 45, rise) - fall * 20);
                Quaternion aUaL = E(Mathf.Lerp(-30, 25, fall), 0, -Mathf.Lerp(25, 55, fall)), aUaR = E(Mathf.Lerp(-55, 15, fall), 0, Mathf.Lerp(20, 55, fall));
                Quaternion aFa = E(-Mathf.Lerp(45, 20, fall));
                thL = Quaternion.Slerp(thL, aThL, air); thR = Quaternion.Slerp(thR, aThR, air);
                shL = Quaternion.Slerp(shL, aShL, air); shR = Quaternion.Slerp(shR, aShR, air);
                uaL = Quaternion.Slerp(uaL, aUaL, air); uaR = Quaternion.Slerp(uaR, aUaR, air);
                faL = Quaternion.Slerp(faL, aFa, air); faR = Quaternion.Slerp(faR, aFa, air);
                ftL = Quaternion.Slerp(ftL, E(20), air); ftR = Quaternion.Slerp(ftR, E(25), air);
                chestRot = Quaternion.Slerp(chestRot, E(Mathf.Lerp(8, -6, fall) + local.z * .6f, 0, bankAngle), air);
                plantFeet = air < .5f;
            }

            // ---- crouch ----
            if (crouch > .01f)
            {
                float k = crouch;
                thL = Quaternion.Slerp(thL, E(-75 + legL * .25f, 0, -8), k); thR = Quaternion.Slerp(thR, E(-75 + legR * .25f, 0, 8), k);
                shL = Quaternion.Slerp(shL, E(110 + kneeL * .2f), k); shR = Quaternion.Slerp(shR, E(110 + kneeR * .2f), k);
                ftL = Quaternion.Slerp(ftL, E(-30), k); ftR = Quaternion.Slerp(ftR, E(-30), k);
                chestRot = Quaternion.Slerp(chestRot, E(28 + breath, 0, 0), k);
                headRot = Quaternion.Slerp(headRot, E(-22), k);
                uaL = Quaternion.Slerp(uaL, E(-35 + armL * .3f, 0, -12), k); uaR = Quaternion.Slerp(uaR, E(-35 + armR * .3f, 0, 12), k);
                faL = Quaternion.Slerp(faL, E(-50), k); faR = Quaternion.Slerp(faR, E(-50), k);
            }

            // ---- traversal states override ----
            float t = Time.time;
            if (player.Hanging)
            {
                float shimmy = player.Action.Contains("hmmy") ? Mathf.Sin(t * 9) : 0;
                uaL = E(-172, 0, -8 + shimmy * 10); uaR = E(-172, 0, 8 + shimmy * 10); faL = faR = E(-8);
                chestRot = E(-6, 0, shimmy * 4); headRot = E(-25);
                thL = E(-18 + shimmy * 14, 0, -4); thR = E(8 - shimmy * 14, 0, 4); shL = E(35); shR = E(20);
                ftL = ftR = E(25); plantFeet = false;
            }
            else if (player.Traversing)
            {
                // Pull with the arms, tuck the knee over, then press up and step through.
                bool vault = player.Action.EndsWith("ault");
                float p = player.TraverseProgress, push = Mathf.SmoothStep(0, 1, (p - .35f) / .45f), step = Mathf.SmoothStep(0, 1, (p - .75f) / .25f);
                if (vault)
                {
                    uaL = E(Mathf.Lerp(-70, -20, push), 0, -25); uaR = E(Mathf.Lerp(-30, 10, push), 0, 40); faL = faR = E(-10);
                    chestRot = E(Mathf.Lerp(32, 10, step), 0, -10); headRot = E(-10);
                    thL = E(Mathf.Lerp(-80, -20, step), 0, -25); thR = E(Mathf.Lerp(-35, 5, step), 0, 20); shL = E(Mathf.Lerp(95, 25, step)); shR = E(Mathf.Lerp(40, 15, step));
                }
                else
                {
                    uaL = E(Mathf.Lerp(-150, -20, push), 0, -14); uaR = E(Mathf.Lerp(-140, -25, push), 0, 14);
                    faL = faR = E(Mathf.Lerp(-40, -80, push * (1 - step)));
                    chestRot = E(Mathf.Lerp(18, 35, push) - step * 25); headRot = E(Mathf.Lerp(-25, -5, push));
                    thL = E(Mathf.Lerp(-100, -15, step), 0, -10); thR = E(Mathf.Lerp(-35, 0, step), 0, 10);
                    shL = E(Mathf.Lerp(120, 20, step)); shR = E(Mathf.Lerp(80, 10, step));
                }
                ftL = ftR = E(-10); plantFeet = false;
            }
            else if (player.Leaping)
            {
                uaL = E(-175, 0, -10); uaR = E(-160, 0, 18); faL = faR = E(-6);
                chestRot = E(-12); headRot = E(-30);
                thL = E(-50, 0, -6); thR = E(5, 0, 6); shL = E(75); shR = E(30); ftL = ftR = E(20); plantFeet = false;
            }
            else if (player.VineClimbing)
            {
                float climb = Mathf.Sin(t * 6);
                uaL = E(-150 + climb * 25, 0, -12); uaR = E(-150 - climb * 25, 0, 12); faL = E(-30 - climb * 20); faR = E(-30 + climb * 20);
                thL = E(-45 + climb * 28, 0, -6); thR = E(-45 - climb * 28, 0, 6); shL = E(70 - climb * 20); shR = E(70 + climb * 20);
                chestRot = E(8); headRot = E(-20); plantFeet = false;
            }
            else if (player.Swinging)
            {
                float pump = Mathf.Clamp(local.z * 4, -30, 30);
                uaL = E(-170, 0, -6); uaR = E(-170, 0, 6); faL = faR = E(-6);
                chestRot = E(-10 - pump * .3f); headRot = E(-15);
                thL = E(-20 - pump, 0, -3); thR = E(-10 - pump, 0, 3); shL = E(40 + pump * .5f); shR = E(25 + pump * .5f);
                ftL = ftR = E(30); plantFeet = false;
            }
            else if (player.WallRunning)
            {
                float side = Vector3.Dot(transform.right, player.WallNormal);
                float wr = Mathf.Sin(t * 15);
                thL = E(-55 * wr, 0, -3); thR = E(55 * wr, 0, 3); shL = E(40 + 50 * Mathf.Max(0, wr)); shR = E(40 + 50 * Mathf.Max(0, -wr));
                uaL = E(30 * wr, 0, -35); uaR = E(-30 * wr, 0, 35); faL = faR = E(-60);
                hipsRot = E(0, 0, -side * 22); chestRot = E(18, 0, -side * 10); plantFeet = false;
            }
            else if (player.Swimming)
            {
                float stroke = t * 5.2f;
                hipsRot = E(72, 0, Mathf.Sin(stroke) * 8); chestRot = E(-10); headRot = E(-55, Mathf.Sin(stroke) * 25, 0);
                uaL = E(-Mathf.Repeat(stroke * Mathf.Rad2Deg, 360), 0, -15); uaR = E(-Mathf.Repeat(stroke * Mathf.Rad2Deg + 180, 360), 0, 15);
                faL = faR = E(-10);
                thL = E(Mathf.Sin(stroke * 1.6f) * 18, 0, -4); thR = E(-Mathf.Sin(stroke * 1.6f) * 18, 0, 4); shL = shR = E(12);
                ftL = ftR = E(50); plantFeet = false; hipDrop = .02f;
            }
            if (player.Action == "Roll")
            {
                float r = player.RollProgress;
                hipsRot = E(r * 360) * hipsRot; chestRot = E(55); headRot = E(-35);
                uaL = E(-60, 0, -25); uaR = E(-60, 0, 25); faL = faR = E(-90);
                thL = E(-110); thR = E(-100); shL = shR = E(130); ftL = ftR = E(-20); plantFeet = false; hipDrop = -.45f * Mathf.Sin(r * Mathf.PI);
            }
            if (player.Dashing)
            {
                hipsRot = E(60); chestRot = E(10); headRot = E(-50);
                uaL = E(-160, 0, -10); uaR = E(20, 0, 25); faL = faR = E(-5);
                thL = E(8); thR = E(20); shL = E(20); shR = E(50); plantFeet = false;
            }
            if (player.Grappling)
            {
                var toward = player.Hand.Point - shoulderR();
                var dir = transform.InverseTransformDirection(toward.normalized);
                uaR = Quaternion.FromToRotation(Vector3.down, dir); faR = E(-5);
                uaL = E(-40, 0, -45); faL = E(-40);
                if (!grounded)
                {
                    chestRot = E(-Mathf.Clamp(player.VerticalSpeed * 1.2f, -18, 18), 0, Mathf.Clamp(-dir.x * 12, -12, 12));
                    thL = E(-30, 0, -5); thR = E(-5, 0, 5); shL = E(60); shR = E(35); plantFeet = false;
                }
            }

            // ---- landing absorb ----
            if (impact > 0 && plantFeet)
            {
                float k = Mathf.Sin(Mathf.Clamp01(impact) * Mathf.PI * .5f);
                thL *= E(-38 * k); thR *= E(-38 * k); shL *= E(70 * k); shR *= E(70 * k);
                ftL *= E(-30 * k); ftR *= E(-30 * k); chestRot *= E(18 * k);
                uaL *= E(-20 * k, 0, -15 * k); uaR *= E(-20 * k, 0, 15 * k);
            }

            // ---- apply: joints ease toward the new pose so state changes flow instead of popping ----
            if (player.Zipping) { chestRot = E(-20); thL = E(-60, 0, -6); thR = E(-20, 0, 6); shL = E(95); shR = E(50); plantFeet = false; }
            var targets = new[] { hipsRot, chestRot, thL, thR, shL, shR, ftL, ftR, uaL, uaR, faL, faR };
            var joints = new[] { hips, chest, thighL, thighR, shinL, shinR, footL, footR, upperArmL, upperArmR, forearmL, forearmR };
            if (blended == null) blended = (Quaternion[])targets.Clone();
            float blend = player.Action == "Roll" ? 1 : 1 - Mathf.Exp(-poseBlend * dt);
            for (int i = 0; i < joints.Length; i++) { blended[i] = Quaternion.Slerp(blended[i], targets[i], blend); joints[i].localRotation = blended[i]; }
            handL.localRotation = E(-10 - elbow * .2f, 0, -8); handR.localRotation = E(-10 - elbow * .2f, 0, 8);

            // Head looks where the camera looks, within reach of the neck.
            var cam = Camera.main;
            Quaternion look = Quaternion.identity;
            if (cam && (grounded || player.Swinging) && !player.Swimming && !player.Hanging)
            {
                var f = transform.InverseTransformDirection(cam.transform.forward);
                float lookYaw = Mathf.Clamp(Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg, -55, 55) * (Mathf.Abs(Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg) < 120 ? 1 : 0);
                float lookPitch = Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(f.y, -1, 1)) * Mathf.Rad2Deg * .5f, -20, 25);
                if (idleTime > 4) lookYaw += Mathf.Sin(idleTime * .45f) * 35 * Mathf.Clamp01(idleTime - 4);
                look = E(lookPitch, lookYaw * (1 - run * .6f), 0);
            }
            headLook = Quaternion.Slerp(headLook, look, 1 - Mathf.Exp(-5 * dt));
            head.localRotation = headRot * headLook;

            // Feet stay on the ground: drop the hips by however much the planted leg is shortened.
            float LegHeight(Quaternion hip, Quaternion thigh, Quaternion shin)
            {
                var r1 = hip * thigh; var r2 = r1 * shin;
                return -(r1 * Vector3.down).y * Thigh + -(r2 * Vector3.down).y * Shin + Ankle;
            }
            float reach = Mathf.Max(LegHeight(blended[0], blended[2], blended[4]), LegHeight(blended[0], blended[3], blended[5]));
            float bob = plantFeet ? Mathf.Max(reach - HipHeight, -.07f - .05f * run - .45f * crouch - .2f * impact) : 0;
            if (!plantFeet && !player.Swimming && !player.Hanging) bob = Mathf.Max(bob, (reach - HipHeight) * .5f);
            hips.localPosition = hipsRest + Vector3.up * (bob + hipDrop + breath * .006f * (1 - moving));

            // Squash and stretch on the whole rig, volume-preserving.
            float st = stretch + (air > .5f ? Mathf.Clamp(player.VerticalSpeed * .006f, -.04f, .06f) : 0);
            rig.localScale = new Vector3(1 - st * .5f, 1 + st, 1 - st * .5f);

            // ---- secondary motion: cape and scarf ----
            float drag = Mathf.Max(0, local.z) * 2.3f + Mathf.Max(0, -player.VerticalSpeed) * 2.2f - Mathf.Max(0, player.VerticalSpeed) * 1.2f;
            float flutter = Mathf.Sin(t * 11 + Mathf.Sin(t * 3)) * Mathf.Clamp01(speed / 8 + Mathf.Abs(player.VerticalSpeed) / 12) * 5;
            float lift = Mathf.Clamp(drag, -8, 70) + crouch * 8 + (player.Swimming ? 12 : 0);
            float cp = capePitch.Step(lift * .55f + 3 + leanAngle * -.4f, 2.4f, .45f, dt);
            float cr = capeRoll.Step(Mathf.Clamp(-local.x * 3 + yawRate * .03f, -25, 25), 2, .4f, dt);
            float clp = capeLowerPitch.Step(lift * .22f + flutter, 3.2f, .35f, dt);
            capeUpper.localRotation = E(Mathf.Clamp(cp, -5, 42), 0, cr);
            capeLower.localRotation = E(Mathf.Clamp(clp, -8, 28), 0, cr * .5f);
            float sp = scarfPitch.Step(lift * .5f + flutter * 1.4f + 6, 3, .3f, dt);
            float sr = scarfRoll.Step(Mathf.Sin(t * 2.3f) * 10 - local.x * 4, 2.5f, .3f, dt);
            scarfTail.localRotation = E(Mathf.Clamp(sp, -10, 70), 0, sr);

            // ---- blink ----
            blinkTimer -= dt;
            if (blinkTimer <= 0) { blink = .14f; blinkTimer = Random.Range(2.2f, 5.5f); }
            blink -= dt;
            float lid = blink > 0 ? .12f : 1;
            if (eyeL) eyeL.localScale = new Vector3(1, lid, 1);
            if (eyeR) eyeR.localScale = new Vector3(1, lid, 1);
        }
        Vector3 shoulderR() => upperArmR.position;
    }
}
