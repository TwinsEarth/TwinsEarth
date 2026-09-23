using UnityEngine;

namespace PixelToCivilization.Actors
{
    /// <summary>载具运动件驱动：轮式滚动、螺旋桨/旋翼旋转、悬浮器起伏；由 Velocity 驱动。</summary>
    [RequireComponent(typeof(VehicleRig))]
    public class VehicleMotion : MonoBehaviour
    {
        public VehicleRig Rig;
        public float WheelRadius = 0.34f;
        float _hover;
        void LateUpdate()
        {
            if(Rig==null) return;
            float dt=Mathf.Max(Time.deltaTime,0.0001f);
            float speed=new Vector2(Rig.Velocity.x,Rig.Velocity.z).magnitude;
            float roll=speed*dt/WheelRadius*Mathf.Rad2Deg;
            foreach(var w in Rig.Wheels) if(w) w.Rotate(Vector3.right,roll,Space.Self);
            if(Rig.Propeller) Rig.Propeller.Rotate(Vector3.forward,dt*1400f,Space.Self);
            if(Rig.Rotor) Rig.Rotor.Rotate(Vector3.up,dt*900f,Space.Self);
            if(Rig.Kind==VehicleKind.Hover)
            {
                _hover+=dt*2f;
                transform.localPosition += new Vector3(0,Mathf.Sin(_hover)*0.0015f,0);
            }
        }
    }
}
