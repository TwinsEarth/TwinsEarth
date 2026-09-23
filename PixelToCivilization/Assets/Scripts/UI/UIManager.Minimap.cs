using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PixelToCivilization.Core;
using PixelToCivilization.Data;
using PixelToCivilization.World;

namespace PixelToCivilization.UI
{
    /// <summary>
    /// UIManager 小地图部分（V6.1.2）—— 对齐 v5.9.9 独立可最小化小地图：
    /// ①底图为真实地形（海/滩/草原/沙漠/山/雪逐像素烘焙，地形重建后刷新一次缓存）；
    /// ②雷达：旋转扫描线 + 扫描半径内建筑/船/敌/资源高亮闪烁；③点击任意点平滑跳转相机。
    /// </summary>
    public partial class UIManager
    {
        private const int MmSize = 160;
        private RawImage _mmImage;
        private Texture2D _mmTex;
        private Color[] _mmBuffer;
        private Color[] _mmBase;                 // 地形底图缓存（静态）
        private RectTransform _mmBox;
        private GameObject _mmBody;
        private float _mmCd;
        private bool _mmMinimized;
        private CameraRig _mmRig;
        private WorldGenerator _mmTerrain;
        private float _radarSweep;               // 雷达扫描角（弧度，现实时间）

        private void BuildMinimap(Transform parent)
        {
            var panel=UITheme.Panel("Minimap",parent,UITheme.PanelBg);
            _mmBox=panel.GetComponent<RectTransform>();
            _mmBox.anchorMin=_mmBox.anchorMax=new Vector2(1,1);_mmBox.pivot=new Vector2(1,1);
            _mmBox.anchoredPosition=new Vector2(-260,-58);_mmBox.sizeDelta=new Vector2(180,206); // v5.9.9 right260 top58 宽180
            var vl=panel.AddComponent<VerticalLayoutGroup>();vl.spacing=3;vl.padding=new RectOffset(6,6,6,6);
            vl.childControlWidth=true;vl.childForceExpandWidth=true;

            var head=UITheme.Panel("MmHead",panel.transform,new Color(0,0,0,0));
            head.AddComponent<LayoutElement>().preferredHeight=22;
            UITheme.Label("mmt",head.transform,"雷达地图",13,TextAnchor.MiddleLeft,UITheme.Gold);
            var minBtn=UITheme.Btn("mmmin",head.transform,"—",12);
            var mbrt=minBtn.GetComponent<RectTransform>();mbrt.anchorMin=mbrt.anchorMax=new Vector2(1,0.5f);mbrt.pivot=new Vector2(1,0.5f);
            mbrt.sizeDelta=new Vector2(24,20);mbrt.anchoredPosition=Vector2.zero;
            minBtn.onClick.AddListener(()=>{_mmMinimized=!_mmMinimized;_mmBody.SetActive(!_mmMinimized);_mmBox.sizeDelta=new Vector2(184,_mmMinimized?34:210);});

            _mmBody=UITheme.Panel("MmBody",panel.transform,new Color(0,0,0,0.4f));
            _mmBody.AddComponent<LayoutElement>().preferredHeight=MmSize;
            var go=new GameObject("MmRaw");go.transform.SetParent(_mmBody.transform,false);
            _mmImage=go.AddComponent<RawImage>();
            var rt=_mmImage.rectTransform;rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=Vector2.zero;rt.offsetMax=Vector2.zero;
            _mmTex=new Texture2D(MmSize,MmSize,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Point};
            _mmBuffer=new Color[MmSize*MmSize];
            _mmBase=new Color[MmSize*MmSize];
            _mmImage.texture=_mmTex;
            var btn=go.AddComponent<Button>();btn.transition=Selectable.Transition.None;
            btn.onClick.AddListener(OnMinimapClick);
        }

        // 世界坐标→小图像素
        private bool W2M(float wx,float wz,out int px,out int py)
        {
            float aw=MmWorld(); float u=wx/aw+0.5f, v=wz/aw+0.5f;
            px=Mathf.RoundToInt(u*MmSize);py=Mathf.RoundToInt(v*MmSize);
            return u>=0&&u<=1&&v>=0&&v<=1;
        }
        private void Plot(float wx,float wz,Color c,int r=1)
        {
            if(!W2M(wx,wz,out int cx,out int cy))return;
            // V6.1.7 战争迷雾：主世界尚未随年代显现的隔海大陆/岛屿，其上任何点都不显示（副本网格不裁剪）
            EnsureTerrain();
            if(_mmTerrain!=null && (S==null||S.CurrentMap=="main"))
            {
                float rr=_mmTerrain.RevealRadius+2f;
                if(wx*wx+wz*wz>rr*rr)return;
            }
            for(int dy=-r;dy<=r;dy++)for(int dx=-r;dx<=r;dx++)
            {
                int x=cx+dx,y=cy+dy;
                if(x<0||y<0||x>=MmSize||y>=MmSize)continue;
                _mmBuffer[y*MmSize+x]=c;
            }
        }

        /// <summary>地形重建后调用：重新烘焙地形底图缓存</summary>
        public void InvalidateMinimapBase(){ _mmBase=null; }

        private void EnsureTerrain(){ if(_mmTerrain==null)_mmTerrain=Object.FindObjectOfType<WorldGenerator>(); }
        private float MmWorld(){ EnsureTerrain(); return _mmTerrain!=null?_mmTerrain.ActiveWorld:GameConstants.WorldSize; } // V6.3.7(真扩展) 小地图跟随真实活动疆域

        private void BakeBase()
        {
            // V6.1.2 修复：InvalidateMinimapBase 会把 _mmBase 置空，这里必须保证重新分配（否则后续 Array.Copy/SetPixels 收到 null）
            if (_mmBase==null || _mmBase.Length!=MmSize*MmSize) _mmBase=new Color[MmSize*MmSize];
            if (_mmBuffer==null || _mmBuffer.Length!=MmSize*MmSize) _mmBuffer=new Color[MmSize*MmSize];
            EnsureTerrain();
            Color ocean=new(0.05f,0.13f,0.24f);
            for(int py=0;py<MmSize;py++)for(int px=0;px<MmSize;px++)
            {
                Color c;
                if(_mmTerrain!=null)
                {
                    float wx=(px/(float)MmSize-0.5f)*MmWorld();
                    float wz=(py/(float)MmSize-0.5f)*MmWorld();
                    c=_mmTerrain.MapColorAt(wx,wz);
                }
                else c=ocean;
                // 略微压暗作为底图，突出上层实体点
                c*=0.82f;
                _mmBase[py*MmSize+px]=c;
            }
        }

        private void RefreshMinimap(float dt)
        {
            if(_mmImage==null||_mmMinimized)return;
            try
            {
                // V6.1.2 防御：纹理/数组任一缺失则重建，绝不向 native 传 null
                if(_mmTex==null)return;
                if(_mmBase==null||_mmBuffer==null)BakeBase();
                if(_mmBase==null||_mmBuffer==null)return;
                _mmCd-=dt; if(_mmCd>0)return;_mmCd=0.18f;
                if(_mmBase==null)BakeBase();
                System.Array.Copy(_mmBase,_mmBuffer,_mmBase.Length);
                _radarSweep += dt*1.6f; if(_radarSweep>Mathf.PI*2f)_radarSweep-=Mathf.PI*2f;

                // 雷达扫描扇形亮带（绕中心旋转，宽约 35°）
                DrawRadarSweep();

                // V6.1.3 各国国都（国色方点，玩家国都大一号），先于建筑金点绘制
                if (S.Nations!=null)
                    foreach (var n in S.Nations)
                        if (n.Alive) Plot(n.Cx,n.Cz,n.Color,n.IsPlayer?2:1);

                // 建筑（金）
                foreach(var b in S.Buildings) Plot(b.X,b.Z,UITheme.Hex(0xffd76b),1);
                // V6.1.5 海外殖民地（青蓝，等级越高点越大）
                var colC=UITheme.Hex(0x35e0e0);
                foreach(var col in S.Colonies) Plot(col.X,col.Z,colC,col.Level>=3?2:1);
                // 车辆（浅青灰）
                foreach(var c in S.Carts) Plot(c.X,c.Z,UITheme.Hex(0x9fd3e6),0);
                // 我方船只/舰队（橙）
                var ours=new Color(1f,0.55f,0.1f);
                foreach(var s in S.Ships) RadarBlink(s.X,s.Z,ours);
                foreach(var f in S.OceanFleets) Plot(f.X,f.Z,ours,1);
                // 敌方船只（红，雷达扫到高亮）
                var enemy=new Color(0.95f,0.25f,0.25f);
                if(GM.Naval!=null) foreach(var e in GM.Naval.EnemyShips) RadarBlink(e.X,e.Z,enemy);
                // 副本资源点/传送点（青=海洋，金=太空）
                var node = S.CurrentMap=="space"?UITheme.Hex(0xffd700):UITheme.Hex(0x35e0e0);
                foreach(var n in S.ResourceNodes) Plot(n.X,n.Z,node,1);
                // 相机/视野中心（白框）
                PlotViewCenter();
                _mmTex.SetPixels(_mmBuffer);_mmTex.Apply(false);
            }
            catch(System.Exception e)
            {
                // 小地图绝不能影响主循环/输入：出错一次后作废底图，下一帧重建，且不刷屏
                if(_mmErrLog<3){_mmErrLog++;Debug.LogWarning("[Minimap] refresh skipped: "+e.Message);}
                _mmBase=null;_mmCd=0.5f;
            }
        }
        private int _mmErrLog;

        // 雷达：实体在扫描线附近 ±20° 时增亮闪烁
        private void RadarBlink(float wx,float wz,Color baseC)
        {
            if(!W2M(wx,wz,out int px,out int py))return;
            float dx=px-MmSize*0.5f, dy=py-MmSize*0.5f;
            float ang=Mathf.Atan2(dy,dx); if(ang<0)ang+=Mathf.PI*2f;
            float diff=Mathf.Abs(Mathf.DeltaAngle(ang*Mathf.Rad2Deg,_radarSweep*Mathf.Rad2Deg));
            Color c=baseC;
            if(diff<18f) c=Color.Lerp(baseC,Color.white,1f-diff/18f); // 扫到瞬间泛白
            Plot(wx,wz,c,1);
        }

        private void DrawRadarSweep()
        {
            int cx=MmSize/2,cy=MmSize/2,R=MmSize/2-1;
            float half=17f*Mathf.Deg2Rad;
            for(int py=0;py<MmSize;py++)for(int px=0;px<MmSize;px++)
            {
                float dx=px-cx,dy=py-cy; float d=Mathf.Sqrt(dx*dx+dy*dy);
                if(d>R)continue;
                float ang=Mathf.Atan2(dy,dx);if(ang<0)ang+=Mathf.PI*2f;
                float diff=Mathf.Abs(Mathf.DeltaAngle(ang*Mathf.Rad2Deg,_radarSweep*Mathf.Rad2Deg))*Mathf.Deg2Rad;
                if(diff<half)
                {
                    float a=(1f-diff/half)*0.35f*(1f-d/R);
                    int idx=py*MmSize+px;
                    _mmBuffer[idx]=Color.Lerp(_mmBuffer[idx],new Color(0.45f,1f,0.6f),a);
                }
            }
            // 扫描亮线
            for(int r=0;r<R;r++)
            {
                int x=cx+Mathf.RoundToInt(Mathf.Cos(_radarSweep)*r);
                int y=cy+Mathf.RoundToInt(Mathf.Sin(_radarSweep)*r);
                if(x>=0&&y>=0&&x<MmSize&&y<MmSize)_mmBuffer[y*MmSize+x]=new Color(0.6f,1f,0.7f,0.9f);
            }
        }

        private void PlotViewCenter()
        {
            if(_mmRig==null)_mmRig=Object.FindObjectOfType<CameraRig>();
            Vector3 c = _mmRig!=null && _mmRig.Target!=null ? _mmRig.Target.position : Vector3.zero;
            if(!W2M(c.x,c.z,out int px,out int py))return;
            for(int dy=-2;dy<=2;dy++)for(int dx=-2;dx<=2;dx++)
            {
                if(Mathf.Abs(dx)!=2&&Mathf.Abs(dy)!=2)continue;
                int x=px+dx,y=py+dy;
                if(x<0||y<0||x>=MmSize||y>=MmSize)continue;
                _mmBuffer[y*MmSize+x]=Color.white;
            }
        }

        private void OnMinimapClick()
        {
            if(_mmImage==null)return;
            if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _mmImage.rectTransform,Input.mousePosition,null,out var local))return;
            var size=_mmImage.rectTransform.rect.size;
            float u=(local.x+size.x*0.5f)/size.x, v=(local.y+size.y*0.5f)/size.y;
            float aw=MmWorld(); float wx=(u-0.5f)*aw, wz=(v-0.5f)*aw;
            if(_mmRig==null)_mmRig=Object.FindObjectOfType<CameraRig>();
            // V6.1.2：平滑跳转（JumpTo 内部换算相对 Target 的偏移，兼容随机村址）
            if(_mmRig!=null)_mmRig.JumpTo(new Vector3(wx,0,wz));
        }
    }
}
