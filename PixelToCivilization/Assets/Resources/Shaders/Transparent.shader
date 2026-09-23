// 像素到文明 · 通用半透明（能量罩等，内置/URP/团结引擎通用）
Shader "PxC/Transparent"
{
    Properties { _Color ("Color", Color) = (0,0.6,1,0.2) }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        LOD 100
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Color;
            struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; };
            struct v2f { float4 pos:SV_POSITION; fixed a:TEXCOORD0; };
            v2f vert(appdata v){ v2f o; o.pos=UnityObjectToClipPos(v.vertex);
                o.a=saturate(dot(normalize(v.normal),normalize(float3(0.45,1,0.35))))*0.5+0.5; return o; }
            fixed4 frag(v2f i):SV_Target{ return fixed4(_Color.rgb,_Color.a*i.a); }
            ENDCG
        }
    }
}
