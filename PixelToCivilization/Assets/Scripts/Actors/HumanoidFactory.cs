using UnityEngine;

namespace PixelToCivilization.Actors
{
    /// <summary>人形骨骼引用（代码驱动动画用）</summary>
    public class HumanoidRig : MonoBehaviour
    {
        public Transform hip, chest, head;
        public Transform armL, armR, foreL, foreR;
        public Transform legL, legR, calfL, calfR;
        public float WalkPhase;
        public bool Moving;
        public float SpeedScale = 1f;
    }

    /// <summary>
    /// V7.0.2 程序化 Q 版人形工厂：大头 / 红衣 / 大腹敦实工人 / 手持工具；
    /// 外观随【年龄(幼/壮/老) · 时代(八时代) · 职业 · 社会阶层 · 个体颜色(种子)】差异化并可成长重建。
    /// 由 HumanoidAnimator 代码驱动行走/待机，无需 AnimatorController。
    /// </summary>
    public static class HumanoidFactory
    {
        // IL2CPP 裁剪保护
        private static readonly System.Type[] __KeepColliders =
        { typeof(CapsuleCollider), typeof(SphereCollider), typeof(BoxCollider), typeof(MeshCollider) };

        static Material _dark, _metal, _wood, _gold, _fur;
        static Material Dark => _dark ??= World.ShaderHelper.Pbr(new Color(0.22f,0.19f,0.16f),0f,0.26f,702,1.0f);
        static Material Metal => _metal ??= World.ShaderHelper.Pbr(new Color(0.62f,0.68f,0.74f),0.6f,0.55f,703,0.7f);
        static Material Wood => _wood ??= World.ShaderHelper.Pbr(new Color(0.55f,0.38f,0.22f),0f,0.24f,704,1.2f);
        static Material Gold => _gold ??= World.ShaderHelper.Pbr(new Color(0.96f,0.78f,0.28f),0.9f,0.72f,705,0.6f);
        static Material Fur => _fur ??= World.ShaderHelper.Pbr(new Color(0.46f,0.34f,0.22f),0f,0.12f,706,1.3f);
        static Material Mat(Color c,float metal=0f,float rough=1.05f)
            => World.ShaderHelper.Pbr(c,metal,0.22f,Mathf.RoundToInt(c.r*999+c.g*777+c.b*555),rough);
        static Material Cloth(Color c)=> World.ShaderHelper.Pbr(c,0f,0.18f,Mathf.RoundToInt(c.r*99+c.g*77+c.b*55),1.1f);

        // 年龄阶段：0 幼年 / 1 壮年(成年) / 2 老年
        public static int StageOf(int age) => age<=13 ? 0 : (age>=60 ? 2 : 1);

        // 兼容旧签名（士兵/旧调用）：默认壮年、时代0、中性种子
        public static HumanoidRig Build(GameObject root, Color outfit, float scale=1f, bool armored=false)
            => Build(root,outfit,scale,armored,null,null,1,0,0,30);
        public static HumanoidRig Build(GameObject root, Color outfit, float scale, bool armored, string job, string socialClass)
            => Build(root,outfit,scale,armored,job,socialClass,1,0,0,30);

        /// <summary>在 root 下搭建 Q 版人形（站立朝向 +Z）。</summary>
        /// <param name="lifeStage">0幼/1壮/2老</param><param name="era">时代 0..7</param>
        /// <param name="colorSeed">个体稳定随机种子（肤色/发色/衣色微调/饰色）</param><param name="ageYears">年龄</param>
        public static HumanoidRig Build(GameObject root, Color outfit, float scale, bool armored, string job,
                                        string socialClass, int lifeStage, int era, int colorSeed, int ageYears)
        {
            if(string.IsNullOrEmpty(job)) job="idle";
            if(string.IsNullOrEmpty(socialClass)) socialClass="commoner";
            era=Mathf.Clamp(era,0,7);
            var rig=root.AddComponent<HumanoidRig>();

            // —— 个体颜色（由稳定种子派生，成长重建后保持同一人）——
            int seed=colorSeed!=0?colorSeed:12345;
            Color skinC=new[]{ new Color(0.97f,0.80f,0.64f), new Color(0.91f,0.71f,0.55f), new Color(0.83f,0.63f,0.47f)}[seed%3];
            Color hairC = lifeStage==2 ? new Color(0.78f,0.77f,0.74f)                          // 老年白发
                      : new[]{ new Color(0.16f,0.11f,0.08f), new Color(0.30f,0.20f,0.12f), new Color(0.10f,0.09f,0.08f)}[seed%3];
            var skin=Mat(new Color(skinC.r,skinC.g,skinC.b),0f,0.85f);
            var hair=Mat(hairC,0f,1.0f);

            // 衣色：时代色调 → 个体明度/色相微抖动（仍保持职业可辨识）
            Color clothC=EraTint(outfit,era,armored);
            Color.RGBToHSV(clothC,out float hh,out float ss,out float vv);
            hh+=(seed%7-3)*0.012f; vv=Mathf.Clamp01(vv+(seed%5-2)*0.022f);
            clothC=Color.HSVToRGB((hh+1f)%1f,Mathf.Clamp01(ss),vv);
            var cloth=Cloth(clothC);
            // 个体饰色（腰带/围巾）：明亮玩具色板
            Color accentC=new[]{ new Color(0.98f,0.75f,0.18f),new Color(0.20f,0.72f,0.95f),new Color(0.45f,0.80f,0.30f),
                                 new Color(0.95f,0.55f,0.12f),new Color(0.85f,0.35f,0.75f),new Color(0.95f,0.95f,0.92f)}[(seed/7)%6];
            var accent=Cloth(accentC);

            // —— 阶段体型 / 阶层体型 ——
            float stageMul = lifeStage==0?0.64f : lifeStage==2?0.93f : 1f;   // 幼年矮小、老年略缩
            float headMul  = lifeStage==0?1.42f : lifeStage==2?1.04f : 1.20f; // Q版大头，幼年头更大
            float classMul = socialClass switch{ "noble"=>1.10f, "rich"=>1.05f, "slave"=>0.92f, _=>1f };
            root.transform.localScale=Vector3.one*scale*stageMul*classMul;
            bool isSoldier = armored || job=="soldier";
            bool isGentry  = socialClass=="noble"||socialClass=="rich"||job=="official"||job=="merchant";

            // —— 髋 / 躯干（敦实 + 大腹）——
            rig.hip=Node(root.transform,"Hip",new Vector3(0,0.95f,0));
            Part(rig.hip,"Pelvis",PrimitiveType.Capsule,cloth,new Vector3(0,-0.02f,0),new Vector3(0.30f,0.14f,0.21f));
            rig.chest=Node(rig.hip,"Chest",new Vector3(0,0.32f,0));
            Part(rig.chest,"Torso",PrimitiveType.Capsule,cloth,new Vector3(0,0.05f,0),new Vector3(0.30f,0.28f,0.22f));
            // 大腹（非士兵；平民/老者更圆润，幼年小肚子可爱，贵族较收敛）
            if(!isSoldier)
            {
                float belly = lifeStage==2?1.12f : isGentry?0.82f : lifeStage==0?0.72f : 1.0f;
                Part(rig.chest,"Belly",PrimitiveType.Sphere,cloth,new Vector3(0,-0.06f,0.15f),new Vector3(0.22f*belly,0.23f*belly,0.17f*belly));
            }
            // 腰带（个体饰色，强化玩具感）
            Part(rig.hip,"Sash",PrimitiveType.Cube,accent,new Vector3(0,0.16f,0),new Vector3(0.34f,0.06f,0.24f));
            if(armored) Part(rig.chest,"Armor",PrimitiveType.Cube,Metal,new Vector3(0,0.08f,0),new Vector3(0.56f,0.36f,0.32f));
            // 贵族/官吏长袍下摆
            if(socialClass=="noble"||job=="official")
                Part(rig.hip,"Robe",PrimitiveType.Cube,cloth,new Vector3(0,-0.18f,0),new Vector3(0.36f,0.28f,0.28f));

            // —— 头 / 颈 ——（大头 Q 版）
            var neck=Node(rig.chest,"Neck",new Vector3(0,0.30f,0));
            rig.head=Node(neck,"Head",new Vector3(0,0.14f,0));
            rig.head.localScale=Vector3.one*headMul;
            Part(rig.head,"HeadMesh",PrimitiveType.Sphere,skin,Vector3.zero,new Vector3(0.19f,0.195f,0.19f));
            Part(rig.head,"Hair",PrimitiveType.Sphere,hair,new Vector3(0,0.055f,-0.012f),new Vector3(0.196f,0.125f,0.196f));
            // 眼睛（Q版两点，提亮表情）
            Part(rig.head,"EyeL",PrimitiveType.Sphere,Dark,new Vector3(-0.07f,0.02f,0.165f),Vector3.one*0.028f);
            Part(rig.head,"EyeR",PrimitiveType.Sphere,Dark,new Vector3( 0.07f,0.02f,0.165f),Vector3.one*0.028f);
            // 鼻子（一点）
            Part(rig.head,"Nose",PrimitiveType.Sphere,skin,new Vector3(0,-0.02f,0.185f),Vector3.one*0.022f);

            // —— 手臂 / 腿（敦实）——
            rig.armL=MakeArm(rig.chest,new Vector3(-0.30f,0.24f,0),cloth,skin,armored,out var fl); rig.foreL=fl;
            rig.armR=MakeArm(rig.chest,new Vector3( 0.30f,0.24f,0),cloth,skin,armored,out var fr); rig.foreR=fr;
            rig.legL=MakeLeg(rig.hip,new Vector3(-0.12f,-0.04f,0),cloth,Dark,out var cl); rig.calfL=cl;
            rig.legR=MakeLeg(rig.hip,new Vector3( 0.12f,-0.04f,0),cloth,Dark,out var cr); rig.calfR=cr;

            // —— 冠帽：职业/阶层优先；返回是否已占头顶 ——
            bool hasHat=AddHeadgear(rig.head,job,socialClass,armored,lifeStage);
            // —— 时代风貌（徽章/毛皮/近现代帽与护目镜）——
            AddEraLook(rig,era,armored,isSoldier,hasHat,ref hasHat,accent,lifeStage);
            if(!hasHat) Part(rig.head,"ClothCap",PrimitiveType.Sphere,hair,new Vector3(0,0.10f,0),new Vector3(0.14f,0.085f,0.14f));

            // —— 手持工具 ——（幼年不持重器；老年平民改拐杖；idle 平民也给锤，保证“红衣工人持工具”）
            if(lifeStage!=0) AddGear(rig,job,clothC,lifeStage);

            var anim=root.AddComponent<HumanoidAnimator>(); anim.Rig=rig;
            return rig;
        }

        // ---------- 时代色调 ----------
        static Color EraTint(Color c,int era,bool armored)
        {
            if(armored) return Color.Lerp(c,new Color(0.42f,0.46f,0.50f),0.35f); // 军服统一偏铁灰
            Color.RGBToHSV(c,out float h,out float s,out float v);
            float hueShift=new[]{0f,-0.02f,0.03f,0.06f,-0.05f,0.08f,0.12f,0.20f}[era];
            float satMul=era>=5?0.82f:1f;
            float vAdj=era<=1?-0.05f:era>=6?0.05f:0f;
            // 未来时代整体偏向洁净高明度
            if(era==7){ s*=0.7f; v=Mathf.Clamp01(v+0.10f); }
            return Color.HSVToRGB((h+hueShift+1f)%1f,Mathf.Clamp01(s*satMul),Mathf.Clamp01(v+vAdj));
        }

        // ---------- 时代风貌：胸章 + 时代专属件 ----------
        static void AddEraLook(HumanoidRig rig,int era,bool armored,bool soldier,bool hadHat,ref bool hasHat,
                               Material accent,int lifeStage)
        {
            // 胸章/徽记（颜色随时代，所有非甲胄平民）
            if(!soldier)
            {
                Color badgeC=era switch{
                    1=>new Color(0.55f,0.12f,0.10f),
                    2=>new Color(0.95f,0.78f,0.28f),
                    3=>new Color(0.30f,0.70f,0.45f),
                    4=>new Color(0.85f,0.70f,0.25f),
                    5=>new Color(0.35f,0.45f,0.60f),
                    6=>new Color(0.78f,0.16f,0.13f),
                    7=>new Color(0.35f,0.85f,0.95f),
                    _=>new Color(0f,0f,0f,0f)};
                if(era>=1)
                {
                    float bs=era==4?0.05f:0.04f;
                    Part(rig.chest,"EraBadge",PrimitiveType.Cube,Cloth(badgeC),new Vector3(0,0.16f,0.21f),new Vector3(bs*1.6f,bs*1.6f,0.03f));
                }
                if(era==0) // 远古兽皮披肩
                {
                    Part(rig.chest,"FurL",PrimitiveType.Cube,Fur,new Vector3(-0.24f,0.22f,0.02f),new Vector3(0.13f,0.10f,0.16f));
                    Part(rig.chest,"FurR",PrimitiveType.Cube,Fur,new Vector3( 0.24f,0.22f,0.02f),new Vector3(0.13f,0.10f,0.16f));
                }
                if(era==7) // 未来护目镜
                    Part(rig.head,"Visor",PrimitiveType.Cube,Mat(new Color(0.10f,0.30f,0.38f),0.4f,0.3f),new Vector3(0,0.03f,0.15f),new Vector3(0.20f,0.07f,0.04f));
            }
            // 无职业帽者：按时代给头饰
            if(!hadHat && !soldier)
            {
                switch(era)
                {
                    case 0: Part(rig.head,"HeadBand",PrimitiveType.Cube,Cloth(new Color(0.55f,0.18f,0.14f)),new Vector3(0,0.075f,0),new Vector3(0.20f,0.04f,0.20f)); hasHat=true; break;
                    case 5: Part(rig.head,"Cap5",PrimitiveType.Cylinder,Cloth(new Color(0.40f,0.48f,0.58f)),new Vector3(0,0.14f,0),new Vector3(0.20f,0.05f,0.20f)); hasHat=true; break;
                    case 6: Part(rig.head,"Cap6",PrimitiveType.Cylinder,Cloth(new Color(0.28f,0.45f,0.30f)),new Vector3(0,0.14f,0),new Vector3(0.20f,0.055f,0.20f)); hasHat=true; break;
                    case 7: Part(rig.head,"Hood7",PrimitiveType.Sphere,Cloth(new Color(0.92f,0.95f,0.98f)),new Vector3(0,0.08f,-0.02f),new Vector3(0.20f,0.14f,0.20f)); hasHat=true; break;
                }
            }
        }

        // ---------- 冠帽（返回是否放置了帽子）----------
        static bool AddHeadgear(Transform head,string job,string cls,bool armored,int lifeStage)
        {
            if(armored||job=="soldier")
            {   // 铁盔 + 红缨
                Part(head,"Helm",PrimitiveType.Sphere,Metal,new Vector3(0,0.09f,0),new Vector3(0.16f,0.13f,0.16f));
                Part(head,"Plume",PrimitiveType.Cube,Cloth(new Color(0.80f,0.16f,0.13f)),new Vector3(0,0.21f,0),new Vector3(0.045f,0.15f,0.045f));
                return true;
            }
            if(job=="official"||cls=="noble")
            {   // 乌纱官帽 + 展角
                Part(head,"OfficialHat",PrimitiveType.Cube,Dark,new Vector3(0,0.13f,0),new Vector3(0.22f,0.11f,0.19f));
                Part(head,"WingL",PrimitiveType.Cube,Dark,new Vector3(-0.17f,0.13f,0),new Vector3(0.13f,0.03f,0.06f));
                Part(head,"WingR",PrimitiveType.Cube,Dark,new Vector3( 0.17f,0.13f,0),new Vector3(0.13f,0.03f,0.06f));
                return true;
            }
            if(lifeStage==0) return false; // 幼童不戴职业帽
            if(job=="farmer"||job=="woodcutter"||job=="miner"||job=="worker")
            {   // 斗笠
                Part(head,"StrawHat",PrimitiveType.Cylinder,Wood,new Vector3(0,0.16f,0),new Vector3(0.38f,0.04f,0.38f));
                return true;
            }
            if(job=="merchant"||cls=="rich")
            {   Part(head,"Fangjin",PrimitiveType.Cube,Dark,new Vector3(0,0.13f,0),new Vector3(0.19f,0.10f,0.17f)); return true; }
            return false; // 交给时代/默认帽
        }

        // ---------- 职业道具 ----------
        static void AddGear(HumanoidRig rig,string job,Color outfit,int lifeStage)
        {
            Transform hand=rig.foreR;
            // 老年平民：拐杖替代重器
            bool cane = lifeStage==2 && job!="soldier" && job!="official" && job!="merchant";
            if(cane){ Part(hand,"Cane",PrimitiveType.Cylinder,Wood,new Vector3(0.06f,-0.5f,0),new Vector3(0.028f,0.62f,0.028f)); return; }
            switch(job)
            {
                case "farmer": // 锄头
                    Part(hand,"HoeHandle",PrimitiveType.Cylinder,Wood,new Vector3(0,-0.42f,0),new Vector3(0.03f,0.5f,0.03f));
                    Part(hand,"HoeHead",PrimitiveType.Cube,Metal,new Vector3(0.08f,-0.85f,0),new Vector3(0.22f,0.06f,0.08f)); break;
                case "woodcutter": // 斧
                    Part(hand,"AxeHandle",PrimitiveType.Cylinder,Wood,new Vector3(0,-0.42f,0),new Vector3(0.035f,0.5f,0.035f));
                    Part(hand,"AxeHead",PrimitiveType.Cube,Metal,new Vector3(0.07f,-0.78f,0),new Vector3(0.18f,0.16f,0.05f)); break;
                case "miner": // 镐
                    Part(hand,"PickHandle",PrimitiveType.Cylinder,Wood,new Vector3(0,-0.42f,0),new Vector3(0.03f,0.5f,0.03f));
                    Part(hand,"PickHead",PrimitiveType.Cube,Metal,new Vector3(0,-0.82f,0),new Vector3(0.3f,0.05f,0.05f)); break;
                case "worker": case "idle": default: // 锤（idle 平民也持工具，即红衣工人）
                    if(job=="idle" && !cane) { /* idle 同样给锤 */ }
                    Part(hand,"HammerHandle",PrimitiveType.Cylinder,Wood,new Vector3(0,-0.4f,0),new Vector3(0.03f,0.45f,0.03f));
                    Part(hand,"HammerHead",PrimitiveType.Cube,Metal,new Vector3(0,-0.72f,0),new Vector3(0.16f,0.1f,0.1f)); break;
                case "soldier": // 长矛 + 盾 + 阵营色军旗
                    Part(hand,"Spear",PrimitiveType.Cylinder,Wood,new Vector3(0,-0.6f,0.05f),new Vector3(0.025f,0.9f,0.025f));
                    Part(hand,"SpearTip",PrimitiveType.Cube,Metal,new Vector3(0,-1.4f,0.05f),new Vector3(0.06f,0.12f,0.06f));
                    Part(rig.chest,"Shield",PrimitiveType.Cube,Metal,new Vector3(-0.3f,0.05f,0.08f),new Vector3(0.06f,0.4f,0.42f));
                    Part(rig.chest,"BannerPole",PrimitiveType.Cylinder,Wood,new Vector3(0.26f,0.12f,-0.14f),new Vector3(0.022f,0.95f,0.022f));
                    Part(rig.chest,"Pennant",PrimitiveType.Cube,Cloth(outfit),new Vector3(0.42f,0.46f,-0.14f),new Vector3(0.32f,0.24f,0.03f)); break;
                case "merchant": // 货袋 + 算盘
                    Part(rig.chest,"Pack",PrimitiveType.Cube,Cloth(new Color(0.55f,0.42f,0.2f)),new Vector3(0,0.05f,-0.22f),new Vector3(0.34f,0.34f,0.2f));
                    Part(hand,"Abacus",PrimitiveType.Cube,Wood,new Vector3(0,-0.45f,0.05f),new Vector3(0.22f,0.04f,0.16f)); break;
                case "official": // 笏板
                    Part(hand,"Scroll",PrimitiveType.Cube,Gold,new Vector3(0,-0.4f,0.06f),new Vector3(0.06f,0.28f,0.16f)); break;
            }
        }

        static Transform MakeArm(Transform parent,Vector3 shoulder,Material cloth,Material skin,bool armored,out Transform fore)
        {
            var upper=Node(parent,"UpperArm",shoulder);
            Part(upper,"Upper",PrimitiveType.Capsule,cloth,new Vector3(0,-0.14f,0),new Vector3(0.082f,0.14f,0.082f));
            if(armored) Part(upper,"Pauldron",PrimitiveType.Sphere,Metal,new Vector3(0,0.02f,0),new Vector3(0.13f,0.10f,0.13f));
            fore=Node(upper,"Forearm",new Vector3(0,-0.28f,0));
            Part(fore,"Fore",PrimitiveType.Capsule,armored?Metal:skin,new Vector3(0,-0.13f,0),new Vector3(0.07f,0.13f,0.07f));
            Part(fore,"Hand",PrimitiveType.Cube,skin,new Vector3(0,-0.28f,0),new Vector3(0.085f,0.09f,0.075f));
            return upper;
        }
        static Transform MakeLeg(Transform parent,Vector3 hipPos,Material cloth,Material dark,out Transform calf)
        {
            var thigh=Node(parent,"Thigh",hipPos);
            Part(thigh,"ThighMesh",PrimitiveType.Capsule,dark,new Vector3(0,-0.22f,0),new Vector3(0.11f,0.22f,0.11f));
            calf=Node(thigh,"Calf",new Vector3(0,-0.44f,0));
            Part(calf,"CalfMesh",PrimitiveType.Capsule,dark,new Vector3(0,-0.22f,0),new Vector3(0.092f,0.22f,0.092f));
            Part(calf,"Foot",PrimitiveType.Cube,dark,new Vector3(0,-0.43f,0.04f),new Vector3(0.11f,0.08f,0.19f));
            return thigh;
        }

        static Transform Node(Transform parent,string name,Vector3 local)
        {
            var go=new GameObject(name); go.transform.SetParent(parent,false);
            go.transform.localPosition=local; return go.transform;
        }
        static GameObject Part(Transform parent,string name,PrimitiveType type,Material mat,Vector3 local,Vector3 scale)
        {
            var go=GameObject.CreatePrimitive(type);
            DestroyCollider(go);
            go.name=name; go.transform.SetParent(parent,false);
            go.transform.localPosition=local; go.transform.localScale=scale;
            var r=go.GetComponent<Renderer>(); r.sharedMaterial=mat;
            r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.On; r.receiveShadows=true;
            return go;
        }
        static void DestroyCollider(GameObject g){var c=g.GetComponent<Collider>();if(c)Object.Destroy(c);}
    }
}
