// 像素到文明 · 顶点色地形着色器（Unlit，内置/URP/团结引擎通用，不依赖光照管线）
Shader "PxC/VertexColor"
{
    Properties { }
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

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 color  : COLOR;
            };
            struct v2f
            {
                float4 pos   : SV_POSITION;
                float4 color : COLOR;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                // 简单方向光明暗，增强地形立体感
                float3 L = normalize(float3(0.45, 1.0, 0.35));
                float ndl = saturate(dot(normalize(v.normal), L)) * 0.45 + 0.55;
                o.color = float4(v.color.rgb * ndl, 1.0);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target { return i.color; }
            ENDCG
        }
    }
    Fallback "Unlit/Color"
}
