using UnityEngine;
using PixelToCivilization.Core;

namespace PixelToCivilization.World
{
    /// <summary>
    /// 轨道相机 —— 对应 Three.js OrbitControls：左键旋转、右键平移、滚轮缩放，带阻尼与边界限制。
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        public Transform Target;
        public float Distance = 860f;            // V6.1.9 世界960×960，开局即看到整片多大陆海洋世界（与雷达小地图一致）
        public float MinDistance = 15f, MaxDistance = 1500f;   // 上限可拉远看全图(世界对角线≈1358)
        public float Yaw = 45f, Pitch = 39f;
        public float MinPitch = 15f, MaxPitch = 85f;
        public float Damping = 0.12f;
        public float PanSpeed = 0.8f, RotateSpeed = 0.25f, ZoomSpeed = 12f;

        private float _yaw,_pitch,_dist;
        private Vector3 _pan;
        // V6.1.2 小地图点击平滑跳转
        private Vector3 _jumpTarget; private bool _jumping;

        private void Start()
        {
            _yaw=Yaw;_pitch=Pitch;_dist=Distance;
            if (Target==null){ var t=new GameObject("CameraTarget"); Target=t.transform; }
        }

        private void LateUpdate()
        {
          try{
            bool overUI = UnityEngine.EventSystems.EventSystem.current!=null &&
                          UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
            bool buildMode = GameManager.Instance!=null &&
                             !string.IsNullOrEmpty(GameManager.Instance.State.SelectedBuildType);
            // 旋转（建造模式或悬停UI时左键不旋转，改为放置/点UI）
            if (Input.GetMouseButton(0) && !Input.GetKey(KeyCode.LeftAlt) && !overUI && !buildMode)
            { _yaw += Input.GetAxis("Mouse X")*RotateSpeed*180f*Time.deltaTime; _pitch -= Input.GetAxis("Mouse Y")*RotateSpeed*180f*Time.deltaTime; }
            // 平移
            if ((Input.GetMouseButton(2) || Input.GetMouseButton(1)) && !overUI)
            {
                _jumping=false;
                var right=Vector3.Cross(Vector3.up, transform.forward).normalized;
                Vector3 move=-right*Input.GetAxis("Mouse X")*PanSpeed*Distance*0.01f
                          -Vector3.Cross(right,Vector3.up)*Input.GetAxis("Mouse Y")*PanSpeed*Distance*0.01f;
                _pan += move;
            }
            // 缩放
            float scroll=Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll)>0.0001f) _dist=Mathf.Clamp(_dist-scroll*ZoomSpeed*Distance*0.1f,MinDistance,MaxDistance);
            _pitch=Mathf.Clamp(_pitch,MinPitch,MaxPitch);

            // 键盘平移 WASD
            Vector3 key=new Vector3(Input.GetAxisRaw("Horizontal"),0,Input.GetAxisRaw("Vertical"));
            if (key.sqrMagnitude>0.01f)
            {
                _jumping=false;
                var yawQ=Quaternion.Euler(0,_yaw,0);
                _pan += yawQ*key*Distance*0.02f;
            }

            // V6.1.2 小地图点击平滑跳转
            if (_jumping)
            {
                _pan=Vector3.Lerp(_pan,_jumpTarget,Mathf.Clamp01(Time.unscaledDeltaTime*4.5f));
                if ((_pan-_jumpTarget).sqrMagnitude<0.4f){_pan=_jumpTarget;_jumping=false;}
            }

            Yaw=Mathf.Lerp(Yaw,_yaw,Damping); Pitch=Mathf.Lerp(Pitch,_pitch,Damping);
            Distance=Mathf.Lerp(Distance,_dist,Damping);
            var rot=Quaternion.Euler(Pitch,Yaw,0);
            transform.position=Vector3.Lerp(transform.position, Target.position+_pan+rot*(Vector3.back*Distance), Damping*2f);
            transform.LookAt(Target.position+_pan);
          }
          catch(System.Exception e){ Debug.LogError("[MARK_CAM] "+e.GetType().Name+": "+e.Message+"\n"+e.StackTrace); }
        }

        /// <summary>瞬切到世界坐标点（相对 Target 偏移）</summary>
        public void FocusOn(Vector3 pos){ _jumping=false; _pan = Target!=null ? pos-Target.position : pos; }
        /// <summary>平滑跳转到世界坐标点（小地图点击）</summary>
        public void JumpTo(Vector3 pos){ _jumpTarget = Target!=null ? pos-Target.position : pos; _jumping=true; }
                /// <summary>村址重建后把 Target 移到新村中心并复位视角</summary>
        public void Retarget(Vector3 villagePos){ if(Target!=null)Target.position=villagePos; _pan=Vector3.zero;_jumping=false; }
        /// <summary>平滑回到视野中心（Target/村址）</summary>
        public void CenterView(){ if(Target!=null){_jumpTarget=Vector3.zero;_jumping=true;} }
    }
}
