// V6.0.1 程序化天空盒：天顶-地平线-地平雾渐变 + 太阳/月光圆盘 + 星空 + 两层程序云。
// 纯 Unlit、不依赖管线光照，URP / Built-in 均可作为 RenderSettings.skybox 使用。
Shader "PxC/SkyDome"
{
    Properties
    {
        _SunDir ("太阳方向(世界)", Vector) = (0.4, 0.7, 0.3, 0)
        _DayFactor ("昼夜系数 0夜1昼", Range(0,1)) = 1
        _ZenithDay ("白天顶", Color) = (0.10, 0.55, 0.92, 1)
        _HorizonDay ("白天地平线", Color) = (0.55, 0.83, 1.00, 1)
        _ZenithNight ("夜晚顶", Color) = (0.012, 0.016, 0.045, 1)
        _HorizonNight ("夜晚地平线", Color) = (0.06, 0.07, 0.12, 1)
        _SunColor ("太阳色", Color) = (1.0, 0.92, 0.74, 1)
        _SunSize ("太阳大小", Range(200,4000)) = 900
        _CloudColor ("云色", Color) = (1,1,1,1)
        _CloudDensity ("云密度", Range(0,2)) = 0.9
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="SkyBox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off ZTest LEqual

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct a2v { float4 pos : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            CBUFFER_START(UnityPerMaterial)
            float4 _SunDir;
            float _DayFactor;
            float4 _ZenithDay, _HorizonDay, _ZenithNight, _HorizonNight;
            float4 _SunColor;
            float _SunSize;
            float4 _CloudColor;
            float _CloudDensity;
            CBUFFER_END

            v2f vert(a2v v)
            {
                v2f o;
                o.pos = TransformObjectToHClip(v.pos.xyz);
                // 天空盒网格的本地坐标即视线方向
                o.dir = normalize(v.pos.xyz);
                return o;
            }

            float hash21(float2 p){ p=frac(p*float2(123.34,456.21)); p+=dot(p,p+45.32); return frac(p.x*p.y); }
            float noise(float2 p){
                float2 i=floor(p), f=frac(p); f=f*f*(3-2*f);
                float a=hash21(i), b=hash21(i+float2(1,0)), c=hash21(i+float2(0,1)), d=hash21(i+1);
                return lerp(lerp(a,b,f.x),lerp(c,d,f.x),f.y);
            }
            float fbm(float2 p){ float v=0,a=0.5; for(int i=0;i<5;i++){v+=a*noise(p);p*=2.02;a*=0.5;} return v; }

            half4 frag(v2f i) : SV_Target
            {
                float3 dir = normalize(i.dir);
                float h = clamp(dir.y*0.5+0.5, 0, 1);
                float t = smoothstep(0.0, 0.55, dir.y);

                float3 zenith = lerp(_ZenithNight.rgb, _ZenithDay.rgb, _DayFactor);
                float3 horizon = lerp(_HorizonNight.rgb, _HorizonDay.rgb, _DayFactor);
                float3 col = lerp(horizon, zenith, pow(t,0.75));

                // 太阳：核心 + 辉光
                float3 sd = normalize(_SunDir.xyz);
                float sun = max(dot(dir, sd), 0);
                float disk = smoothstep(1.0-1.0/_SunSize, 1.0-0.5/_SunSize, sun);
                float glow = pow(sun, 8) * 0.35 + pow(sun, 80) * 0.25;
                col += _SunColor.rgb * (disk * 6.0 + glow) * _DayFactor;
                // 月亮（夜间，反方向弱光）
                float moon = smoothstep(0.9995, 0.9998, max(dot(dir,-sd),0));
                col += float3(0.8,0.85,1.0) * moon * (1-_DayFactor);

                // 星空（仅上半球、夜间）
                if(dir.y>0.02){
                    float2 sp = dir.xz/(dir.y+0.15);
                    float s = hash21(floor(sp*260.0));
                    float star = step(0.9975, s) * (0.5+0.5*sin(_Time.y*3+s*40));
                    col += float3(star,star,star) * (1-_DayFactor) * smoothstep(0.02,0.25,dir.y);
                }

                // 两层程序云（随时间平移）
                float cloudMask = smoothstep(0.02,0.28,dir.y) * _DayFactor;
                float2 cuv = dir.xz/(abs(dir.y)+0.25)*1.4;
                float cl = fbm(cuv*1.3 + float2(_Time.x*0.01, _Time.x*0.006));
                cl = smoothstep(0.55-_CloudDensity*0.15, 0.95, cl) * cloudMask;
                float3 cloudCol = lerp(float3(0.5,0.55,0.65), _CloudColor.rgb, _DayFactor);
                col = lerp(col, cloudCol, cl*0.65);

                // 地平线轻微雾晕
                col = lerp(col, horizon, (1-t)*0.25);
                return half4(col,1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
