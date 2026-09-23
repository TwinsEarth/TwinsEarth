// V6.0.1 PBR 水面（URP 透明）：双层 Gerstner 波、程序流动法线、菲涅尔、
// 基于 _CameraOpaqueTexture 的伪折射、主光 BlinnPhong 高光。
Shader "PxC/WaterURP"
{
    Properties
    {
        _Shallow ("浅水色", Color) = (0.18, 0.52, 0.62, 0.78)
        _Deep ("深水色", Color) = (0.03, 0.16, 0.30, 0.92)
        _SkyTint ("天空反射色", Color) = (0.62, 0.74, 0.86, 1)
        _Smoothness ("光滑度", Range(0,1)) = 0.94
        _WaveAmp ("波幅", Range(0,1)) = 0.18
        _WaveFreq ("波频率", Range(0.1,4)) = 0.9
        _Distort ("折射扰动", Range(0,0.1)) = 0.03
        _FresnelPow ("菲涅尔幂", Range(1,8)) = 3.2
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off Cull Off

        Pass
        {
            Name "WaterForward"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _Shallow, _Deep, _SkyTint;
            float _Smoothness, _WaveAmp, _WaveFreq, _Distort, _FresnelPow;
            CBUFFER_END

            struct a2v { float4 pos:POSITION; float2 uv:TEXCOORD0; };
            struct v2f {
                float4 pos:SV_POSITION; float3 worldPos:TEXCOORD0;
                float2 uv:TEXCOORD1; float4 scr:TEXCOORD2; float fog:TEXCOORD3;
            };

            float hash21(float2 p){ p=frac(p*float2(123.34,456.21)); p+=dot(p,p+45.32); return frac(p.x*p.y); }
            float vnoise(float2 p){
                float2 i=floor(p),f=frac(p); f=f*f*(3-2*f);
                float a=hash21(i),b=hash21(i+float2(1,0)),c=hash21(i+float2(0,1)),d=hash21(i+1);
                return lerp(lerp(a,b,f.x),lerp(c,d,f.x),f.y);
            }
            // 两层波的高度与导数（用于位移与法线）
            float WaveHeight(float2 p, out float2 grad){
                float t=_Time.y;
                float2 d1=float2(1,0.6), d2=float2(-0.5,1);
                float ph1=t*0.9, ph2=t*1.3;
                float a1=sin(dot(p,d1)*_WaveFreq + ph1);
                float a2=sin(dot(p,d2)*_WaveFreq*1.7 + ph2);
                grad.x = cos(dot(p,d1)*_WaveFreq+ph1)*d1.x*_WaveFreq + cos(dot(p,d2)*_WaveFreq*1.7+ph2)*d2.x*_WaveFreq*1.7;
                grad.y = cos(dot(p,d1)*_WaveFreq+ph1)*d1.y*_WaveFreq + cos(dot(p,d2)*_WaveFreq*1.7+ph2)*d2.y*_WaveFreq*1.7;
                return (a1*0.65 + a2*0.35) * _WaveAmp;
            }

            v2f vert(a2v v){
                v2f o;
                float3 wp=TransformObjectToWorld(v.pos.xyz);
                float2 grad;
                // 基础 Plane 网格稀疏，弱化几何起伏，避免放大后出现粗大斜向波棱；细腻波纹交给片元法线
                wp.y += WaveHeight(wp.xz, grad)*0.3;
                o.worldPos=wp; o.uv=v.uv;
                o.pos=TransformWorldToHClip(wp);
                o.scr=ComputeScreenPos(o.pos);
                o.fog=ComputeFogFactor(o.pos.z);
                return o;
            }

            half4 frag(v2f i):SV_Target{
                float2 grad; WaveHeight(i.worldPos.xz,grad);
                // 叠加高频噪声法线
                float2 flowA=i.worldPos.xz*0.35+_Time.y*0.05;
                float2 flowB=i.worldPos.xz*0.8-_Time.y*0.035;
                float micro=(vnoise(flowA)-0.5)*0.18+(vnoise(flowB)-0.5)*0.10;
                float3 N=normalize(float3(-(grad.x+micro)*0.22, 1, -(grad.y+micro)*0.22));
                float3 V=normalize(GetWorldSpaceViewDir(i.worldPos));
                float ndv=max(dot(N,V),0);
                float fres=pow(1-ndv,_FresnelPow);

                // 伪折射：场景色随法线扰动
                float2 sceneUV=i.scr.xy/i.scr.w;
                #if UNITY_UV_STARTS_AT_TOP
                sceneUV.y=1-sceneUV.y;
                #endif
                float3 refr=SampleSceneColor(sceneUV+N.xz*_Distort);

                // 水深：视角越掠射越像深水/反射
                float3 water=lerp(_Deep.rgb,_Shallow.rgb,ndv*0.8);
                float3 col=lerp(lerp(refr,water,_Shallow.a), _SkyTint.rgb, fres*0.7);

                // 主光高光（柔和，避免粗白条纹）
                Light mainL=GetMainLight();
                float3 L=normalize(mainL.direction);
                float3 H=normalize(L+V);
                float spec=pow(max(dot(N,H),0),lerp(60,200,_Smoothness));
                col += mainL.color*spec*0.5;

                float alpha=lerp(_Deep.a, _Shallow.a, ndv*0.7);
                alpha=max(alpha,fres*0.85);
                float4 res=float4(col,alpha);
                #if defined(FOG_LINEAR)||defined(FOG_EXP)||defined(FOG_EXP2)
                res.rgb=MixFog(res.rgb,i.fog);
                #endif
                return half4(res);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
