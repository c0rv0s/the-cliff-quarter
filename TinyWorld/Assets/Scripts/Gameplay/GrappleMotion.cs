using UnityEngine;

namespace TinyWorld
{
    public static class GrappleMotion
    {
        // Nerf's applyGrapplePull: reel inward without erasing sideways momentum.
        public static Vector3 Pull(Vector3 velocity,Vector3 toward,ref float ropeLength,float dt)
        {
            float distance=toward.magnitude;
            if(distance<.2f)return velocity;
            float upwardHelp=toward.y>1?7:0;
            toward/=distance;
            ropeLength=Mathf.Max(4.2f,Mathf.Min(ropeLength>0?ropeLength:distance,distance)-8.5f*dt);
            float stretch=Mathf.Max(0,distance-ropeLength);
            float acceleration=Mathf.Min(74,26+upwardHelp+stretch*5.5f);
            velocity+=toward*(acceleration*dt);
            float radialSpeed=Vector3.Dot(velocity,toward);
            if(stretch>.1f&&radialSpeed<0)velocity+=toward*(-radialSpeed*Mathf.Min(1,dt*11));
            return Vector3.ClampMagnitude(velocity,40);
        }
    }
}
