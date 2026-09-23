// 像素到文明 · 建筑/角色通用着色器（纯色+方向光+自发光，内置/URP/团结引擎通用）
Shader "PxC/Building"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _Emission ("Emission", Color) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Color;
            fixed4 _Emission;
            struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; };
            struct v2f { float4 pos:SV_POSITION; fixed4 col:COLOR; };
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float3 L = normalize(float3(0.45,1.0,0.35));
                float ndl = saturate(dot(normalize(v.normal),L))*0.5+0.5;
                o.col = fixed4(_Color.rgb*ndl + _Emission.rgb, 1.0);
                return o;
            }
            fixed4 frag(v2f i):SV_Target{ return i.col; }
            ENDCG
        }
    }
    Fallback "Unlit/Color"
}
