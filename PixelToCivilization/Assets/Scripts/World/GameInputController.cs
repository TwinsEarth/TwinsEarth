using UnityEngine;
using PixelToCivilization.Core;
using PixelToCivilization.Data;
using PixelToCivilization.Systems;

namespace PixelToCivilization.World
{
    /// <summary>
    /// 输入控制器：建造模式下点击地面放置建筑；选择模式下点击建筑查看信息；Esc取消。
    /// </summary>
    public class GameInputController : MonoBehaviour
    {
        private GameManager _gm;
        private Camera _cam;
        private GameObject _ghost;
        public LayerMask GroundMask=~0;

        private void Start()
        {
            _gm=GameManager.Instance;
            _cam=Camera.main;
        }

        private void Update()
        {
            if (_gm==null || _gm.StateType!=GameStateType.Playing) return;
            if (Input.GetKeyDown(KeyCode.Escape)) { _gm.State.SelectedBuildType=null; _gm.Tool="select"; HideGhost(); }

            Ray ray=_cam!=null?_cam.ScreenPointToRay(Input.mousePosition):new Ray();
            bool buildMode = !string.IsNullOrEmpty(_gm.State.SelectedBuildType);

            if (buildMode)
            {
                UpdateGhost(ray);
                if (Input.GetMouseButtonDown(0) && !PointerOverUI()) TryPlace(ray);
            }
            else if (Input.GetMouseButtonDown(0) && !PointerOverUI() && GroundHit(ray,out var tp))
            {
                // V6.1.2 底部工具：种树 / 招民（对齐 v5.9.9 setTool）
                if (_gm.Tool=="tree") _gm.Env?.PlantTreeAt(tp.x,tp.z);
                else if (_gm.Tool=="npc") _gm.Env?.RecruitAt(tp.x,tp.z);
            }
        }

        private bool PointerOverUI()
        {
            if (UnityEngine.EventSystems.EventSystem.current==null) return false;
            return UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
        }

        private bool GroundHit(Ray ray,out Vector3 p)
        {
            p=default;
            if (Physics.Raycast(ray,out var hit,1000f,GroundMask))
            {
                if (hit.collider.name=="Terrain"){ p=hit.point; return true; }
            }
            return false;
        }

        private void UpdateGhost(Ray ray)
        {
            if (!GroundHit(ray,out var p)){ HideGhost(); return; }
            if (_ghost==null)
            {
                _ghost=GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(_ghost.GetComponent<Collider>());
                _ghost.transform.localScale=new Vector3(3,2.5f,3);
                _ghost.GetComponent<Renderer>().material=ShaderHelper.Trans(new Color(0.3f,1f,0.5f,0.4f));
            }
            _ghost.SetActive(true);
            string st=_gm.State.SelectedBuildType;
            bool ok;
            if (st!=null && st.StartsWith("cart:")) ok=_gm.Cart.CanBuild(st.Substring(5),p.x,p.z,out _); // CanBuild 已含陆地判定
            else if (st!=null && st.StartsWith("ship:")) ok=true;
            else ok=_gm.Building.CanBuild(st,p.x,p.z,out _);
            _ghost.GetComponent<Renderer>().material.color=ok?new Color(0.3f,1f,0.5f,0.4f):new Color(1f,0.3f,0.3f,0.4f);
            _ghost.transform.position=new Vector3(p.x,p.y+1.2f,p.z);
        }

        private void TryPlace(Ray ray)
        {
            if (!GroundHit(ray,out var p)) return;
            string type=_gm.State.SelectedBuildType;
            bool ok;
            // V6.1.1 运输分类：cart:/ship: 前缀分流到车辆/船只系统，其余走建筑
            if (type!=null && type.StartsWith("cart:")) ok=_gm.Cart.BuildCart(type.Substring(5),p.x,p.z);
            else if (type!=null && type.StartsWith("ship:")) ok=_gm.Naval.BuildShip(type.Substring(5),p.x,p.z);
            else ok=_gm.Building.PlaceBuilding(type,p.x,p.z);
            // 连建：按住Shift保持，否则取消选择
            if (ok && !Input.GetKey(KeyCode.LeftShift)) { _gm.State.SelectedBuildType=null; HideGhost(); }
        }

        private void HideGhost(){ if (_ghost!=null) _ghost.SetActive(false); }
    }
}
