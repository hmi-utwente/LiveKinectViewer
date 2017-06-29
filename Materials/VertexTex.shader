// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Custom/VertexTex"
	{
		Properties
		{
			_ParticleSize("Particle Size", Float) = 1.0
			_DepthClip("Depth clip", Float) = 10.0
			_Distance("Distance", Float) = 10.0
			_PositionTex("Position Texture", 2D) = "black" {}
		_ColorTex("Color Texture", 2D) = "black" {}
		_MaskTex("Mask Texture", 2D) = "white" {}
		_Background("Background Texture", 2D) = "white" {}
		}

			SubShader
		{
			Pass
		{
			Cull Off

			CGPROGRAM

#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"

			struct vertexInput
		{
			float4 pos : POSITION;
			float4 uv0 : TEXCOORD0; // quad
			float4 uv1 : TEXCOORD1; // uv pos
			float4 uv2 : TEXCOORD2; // uv col
		};

		struct v2f
		{
			float4 pos : SV_POSITION;
			float4 uv1: TEXCOORD0; // uv pos
			float4 uv2 : TEXCOORD1; // uv col
		};

		float _ParticleSize;
		float _DepthClip;
		float _Distance;
		sampler2D _PositionTex;
		sampler2D _ColorTex;
		sampler2D _MaskTex;
		sampler2D _Background;

		v2f vert(vertexInput v)
		{
			v2f o;
			float4 p = tex2Dlod(_PositionTex, v.uv1);
			p += v.uv0 * _ParticleSize;
			p.z *= 15;
			o.pos = UnityObjectToClipPos(p);
			o.uv1 = v.uv1;
			o.uv2 = v.uv2;
			return o;
		}

		float4 frag(v2f i) : COLOR
		{
			float3 p1 = tex2D(_PositionTex, i.uv1);
	/*		if (p1.z < 0.1)
				discard;*/

			float3 c = tex2D(_ColorTex, i.uv2);
			return float4(c, 1);
		}
			ENDCG
		}
		}
	}