using UnityEngine;

namespace PixelToCivilization.Actors
{
    /// <summary>
    /// V6.1.1 人形代码骨骼动画：无需 AnimatorController，用正弦驱动四肢摆动模拟行走，
    /// 待机时轻微呼吸/重心起伏；外部只需设置 Velocity（世界速度）即可自动切换步态。
    /// </summary>
    [RequireComponent(typeof(HumanoidRig))]
    public class HumanoidAnimator : MonoBehaviour
    {
        public HumanoidRig Rig;
        public Vector3 Velocity;
        public float StrideFreq = 7.5f;     // 步频
        public float MaxSwing = 38f;        // 最大摆腿角度
        public float TurnSpeed = 10f;
        float _idle;

        void LateUpdate()
        {
            if (Rig==null) return;
            float speed = new Vector2(Velocity.x,Velocity.z).magnitude;
            Rig.Moving = speed > 0.02f;
            float dt=Mathf.Max(Time.deltaTime,0.0001f);

            if (Rig.Moving)
            {
                // 朝向速度方向
                Vector3 fwd=Velocity; fwd.y=0;
                if(fwd.sqrMagnitude>0.0001f){
                    var target=Quaternion.LookRotation(fwd);
                    Rig.hip.rotation=Quaternion.Slerp(Rig.hip.rotation,target,TurnSpeed*dt);
                }
                Rig.WalkPhase += dt*StrideFreq*Mathf.Clamp(speed,0.4f,2.2f);
                float swing=Mathf.Sin(Rig.WalkPhase)*MaxSwing*Mathf.Clamp(speed,0.2f,1.4f);
                float bend =Mathf.Abs(Mathf.Cos(Rig.WalkPhase))*34f*Mathf.Clamp(speed,0.2f,1.4f);
                SetX(Rig.legL,swing); SetX(Rig.legR,-swing);
                SetX(Rig.calfL,Mathf.Max(0,-swing)*0.6f+12f);
                SetX(Rig.calfR,Mathf.Max(0, swing)*0.6f+12f);
                SetX(Rig.armL,-swing*0.85f); SetX(Rig.armR,swing*0.85f);
                SetX(Rig.foreL,-20f-Mathf.Abs(Mathf.Sin(Rig.WalkPhase))*12f);
                SetX(Rig.foreR,-20f-Mathf.Abs(Mathf.Cos(Rig.WalkPhase))*12f);
                // 走路起伏
                Rig.hip.localPosition=new Vector3(0,0.95f+Mathf.Abs(Mathf.Sin(Rig.WalkPhase))*0.04f,0);
                if(Rig.chest) Rig.chest.localRotation=Quaternion.Euler(6f,0,Mathf.Sin(Rig.WalkPhase)*2f);
            }
            else
            {
                _idle += dt;
                // 回到中立 + 呼吸
                DampRotation(Rig.legL,Quaternion.identity,dt);DampRotation(Rig.legR,Quaternion.identity,dt);
                DampRotation(Rig.calfL,Quaternion.identity,dt);DampRotation(Rig.calfR,Quaternion.identity,dt);
                DampRotation(Rig.armL,Quaternion.Euler(0,0,6f),dt);DampRotation(Rig.armR,Quaternion.Euler(0,0,-6f),dt);
                DampRotation(Rig.foreL,Quaternion.Euler(-24f,0,0),dt);DampRotation(Rig.foreR,Quaternion.Euler(-24f,0,0),dt);
                float breath=Mathf.Sin(_idle*1.8f)*0.012f;
                Rig.hip.localPosition=new Vector3(0,0.95f+breath,0);
                if(Rig.chest) Rig.chest.localRotation=Quaternion.Euler(4f+breath*8f,0,0);
            }
        }
        static void SetX(Transform t,float deg){ if(t) t.localRotation=Quaternion.Euler(deg,0,0); }
        void DampRotation(Transform t,Quaternion target,float dt){ if(t) t.localRotation=Quaternion.Slerp(t.localRotation,target,10f*dt); }
    }
}
