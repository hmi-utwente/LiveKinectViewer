// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Custom/VertexTex"
{
	Properties
	{
		_ParticleSize("Particle Size", Range(0, 0.015)) = 0.003
		_PositionTex("Position Texture", 2D) = "black" {}
		_ColorTex("Color Texture", 2D) = "black" {}
	}

	SubShader
	{
		Tags{ "RenderType" = "Opaque" }
		LOD 100

		Pass
		{
			Cull Off
			CGPROGRAM
			#pragma vertex vert
			#pragma geometry geo
			#pragma fragment frag
			#include "UnityCG.cginc"

			struct vertexInput
			{
				float4 pos : POSITION;
				float4 uvPos : TEXCOORD0; // uv pos
				float4 uvCol : TEXCOORD1; // uv col
			};

			struct geoInput
			{
				float4 pos : SV_POSITION;
				float4 uvCol : TEXCOORD0; // uv col
			};

			struct fragInput
			{
				float4 pos : SV_POSITION;
				float4 uvCol : TEXCOORD0; // uv col
			};

			float _ParticleSize;
			sampler2D _PositionTex;
			sampler2D _ColorTex;

			geoInput vert(vertexInput v)
			{
				geoInput o;
				float4 p = tex2Dlod(_PositionTex, v.uvPos);

				o.pos = p;
				o.uvCol = v.uvCol;
				return o;
			}

			[maxvertexcount(4)]
			void geo(point geoInput p[1], inout TriangleStream<fragInput> triStream)
			{
				float3 up = float3(0, 1, 0);
				float3 right = float3(1, 0, 0);

				float halfS = 0.5f * _ParticleSize * p[0].pos.z;

				float4 v[4];
				v[0] = float4(-halfS * right - halfS * up, 1.0f);
				v[1] = float4(-halfS * right + halfS * up, 1.0f);
				v[2] = float4(halfS * right - halfS * up, 1.0f);
				v[3] = float4(halfS * right + halfS * up, 1.0f);

				fragInput pIn;

				[loop]
				for (uint i = 0; i < 4; i++)
				{
					pIn.pos = mul(UNITY_MATRIX_P,
						mul(UNITY_MATRIX_MV, p[0].pos)
						+ float4(v[i].x, v[i].y, 0.0, 0.0));
					pIn.uvCol = p[0].uvCol;

					triStream.Append(pIn);
				}
			}

			float4 frag(fragInput i) : COLOR
			{
				float3 c = tex2D(_ColorTex, i.uvCol);
				return float4(c, 1);
			}
			ENDCG
		}
	}
}