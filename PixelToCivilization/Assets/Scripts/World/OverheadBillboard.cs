using UnityEngine;

namespace PixelToCivilization.World
{
    /// <summary>
    /// V6.1.9(i) 单位头顶标识：一根旗杆 + 势力色旗帜 + 与旗帜同色的血条；整体始终面向相机（Billboard）。
    /// 用法：OverheadBillboard.Attach(view, color, hasHp, scale)；运行期 SetColor / SetHp。
    /// </summary>
    public class OverheadBillboard : MonoBehaviour
    {
        private Transform _fill;              // 血条填充（左对齐）
        private Renderer _flagR, _fillR;
        private GameObject _barRoot;
        private float _w = 1.0f;
        private Camera _cam;

        public static OverheadBillboard Attach(GameObject host, Color color, bool hasHp, float scale = 1f, float height = 2.3f)
        {
            if (host == null) return null;
            var go = new GameObject("Overhead");
            go.transform.SetParent(host.transform);
            go.transform.localPosition = new Vector3(0f, height, 0f);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one * scale;
            var oh = go.AddComponent<OverheadBillboard>();
            oh.Build(color, hasHp);
            return oh;
        }

        private void Build(Color color, bool hasHp)
        {
            // 旗杆
            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(pole.GetComponent<Collider>());
            pole.transform.SetParent(transform); pole.transform.localPosition = new Vector3(0, 0.45f, 0);
            pole.transform.localScale = new Vector3(0.04f, 0.45f, 0.04f);
            pole.GetComponent<Renderer>().sharedMaterial = ShaderHelper.Mat(new Color(0.12f, 0.10f, 0.08f));
            // 旗帜
            var flag = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(flag.GetComponent<Collider>());
            flag.transform.SetParent(transform); flag.transform.localPosition = new Vector3(0.28f, 0.78f, 0);
            flag.transform.localScale = new Vector3(0.56f, 0.34f, 0.03f);
            _flagR = flag.GetComponent<Renderer>(); _flagR.material = ShaderHelper.Mat(color);

            // 血条（背景 + 左对齐填充）
            _barRoot = new GameObject("Bar"); _barRoot.transform.SetParent(transform);
            _barRoot.transform.localPosition = new Vector3(0, 1.02f, 0);
            var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(bg.GetComponent<Collider>());
            bg.transform.SetParent(_barRoot.transform); bg.transform.localPosition = Vector3.zero;
            bg.transform.localScale = new Vector3(_w + 0.08f, 0.18f, 1f);
            bg.GetComponent<Renderer>().sharedMaterial = ShaderHelper.Mat(new Color(0.04f, 0.04f, 0.04f, 0.85f));
            var pivot = new GameObject("Pivot"); pivot.transform.SetParent(_barRoot.transform);
            pivot.transform.localPosition = new Vector3(-_w / 2f, 0, 0);   // 左锚点
            var fillGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(fillGo.GetComponent<Collider>());
            fillGo.transform.SetParent(pivot.transform); fillGo.transform.localPosition = new Vector3(_w / 2f, 0, -0.01f);
            fillGo.transform.localScale = new Vector3(_w, 0.12f, 1f);
            _fill = fillGo.transform; _fillR = fillGo.GetComponent<Renderer>(); _fillR.material = ShaderHelper.Mat(color);
            _barRoot.SetActive(hasHp);
        }

        public void SetColor(Color c)
        {
            if (_flagR != null) _flagR.material.color = c;
            if (_fillR != null) _fillR.material.color = c;
        }

        public void SetHp(float ratio)
        {
            ratio = Mathf.Clamp01(ratio);
            if (_fill != null)
            {
                _fill.localScale = new Vector3(Mathf.Max(0.001f, _w * ratio), _fill.localScale.y, 1f);
                _fill.localPosition = new Vector3(_w * ratio / 2f, 0, -0.01f);
            }
        }

        public void SetBarVisible(bool v) { if (_barRoot != null) _barRoot.SetActive(v); }

        private void LateUpdate()
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;
            // 法线从物体指向相机（保持竖直），确保单面 Quad 正面朝向观察者
            Vector3 toCam = _cam.transform.position - transform.position; toCam.y = 0f;
            if (toCam.sqrMagnitude > 1e-5f)
                transform.rotation = Quaternion.LookRotation(toCam.normalized, Vector3.up);
        }
    }
}
