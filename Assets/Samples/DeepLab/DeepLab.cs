﻿
using System.Linq;
using UnityEngine;

namespace TensorFlowLite
{
    public class DeepLab : BaseImagePredictor<float>
    {
        // Port from
        // https://github.com/tensorflow/examples/blob/master/lite/examples/image_segmentation/ios/ImageSegmentation/ImageSegmentator.swift
        private static readonly Color32[] COLOR_TABLE = new Color32[]
        {
            ToColor(0xFF00_0000), // Black
            ToColor(0xFF80_3E75), // Strong Purple
            ToColor(0xFFFF_6800), // Vivid Orange
            ToColor(0xFFA6_BDD7), // Very Light Blue
            ToColor(0xFFC1_0020), // Vivid Red
            ToColor(0xFFCE_A262), // Grayish Yellow
            ToColor(0xFF81_7066), // Medium Gray
            ToColor(0xFF00_7D34), // Vivid Green
            ToColor(0xFFF6_768E), // Strong Purplish Pink
            ToColor(0xFF00_538A), // Strong Blue
            ToColor(0xFFFF_7A5C), // Strong Yellowish Pink
            ToColor(0xFF53_377A), // Strong Violet
            ToColor(0xFFFF_8E00), // Vivid Orange Yellow
            ToColor(0xFFB3_2851), // Strong Purplish Red
            ToColor(0xFFF4_C800), // Vivid Greenish Yellow
            ToColor(0xFF7F_180D), // Strong Reddish Brown
            ToColor(0xFF93_AA00), // Vivid Yellowish Green
            ToColor(0xFF59_3315), // Deep Yellowish Brown
            ToColor(0xFFF1_3A13), // Vivid Reddish Orange
            ToColor(0xFF23_2C16), // Dark Olive Green
            ToColor(0xFF00_A1C2), // Vivid Blue
        };



        // https://www.tensorflow.org/lite/models/segmentation/overview

        private readonly float[,,] outputs0; // height, width, 21

        private readonly ComputeShader compute;
        private readonly ComputeBuffer labelBuffer;
        private readonly ComputeBuffer colorTableBuffer;
        private readonly RenderTexture labelTex;

        private readonly Color32[] labelPixels;
        private readonly Texture2D labelTex2D;
        private readonly int labelToTexKernel;

        public DeepLab(string modelPath, ComputeShader compute) : base(modelPath, true)
        {
            var odim0 = interpreter.GetOutputTensorInfo(0).shape;

            Debug.Assert(odim0[1] == height);
            Debug.Assert(odim0[2] == width);

            outputs0 = new float[odim0[1], odim0[2], odim0[3]];
            labelPixels = new Color32[width * height];
            labelTex2D = new Texture2D(width, height, TextureFormat.RGBA32, 0, false);

            // Init compute sahder resources
            labelTex = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
            labelTex.enableRandomWrite = true;
            labelTex.Create();
            labelBuffer = new ComputeBuffer(height * width, sizeof(float) * 21);
            colorTableBuffer = new ComputeBuffer(21, sizeof(float) * 4);

            int initKernal = compute.FindKernel("Init");
            this.compute = compute;
            compute.SetInt("Width", width);
            compute.SetInt("Height", height);
            compute.SetTexture(initKernal, "Result", labelTex);
            compute.Dispatch(initKernal, width, height, 1);

            labelToTexKernel = compute.FindKernel("LabelToTex");

            // Init RGBA color table
            var table = COLOR_TABLE.Select(c => c.ToRGBA()).ToList();
            colorTableBuffer.SetData(table);
        }

        public override void Dispose()
        {
            base.Dispose();

            if (labelTex != null)
            {
                labelTex.Release();
                Object.Destroy(labelTex);
            }
            if (labelTex2D != null)
            {
                Object.Destroy(labelTex2D);
            }
            labelBuffer?.Release();
            colorTableBuffer?.Release();
        }

        public override void Invoke(Texture inputTex)
        {
            ToTensor(inputTex, input0);

            interpreter.SetInputTensorData(0, input0);
            interpreter.Invoke();
            interpreter.GetOutputTensorData(0, outputs0);
        }

        public RenderTexture GetResultTexture()
        {
            labelBuffer.SetData(outputs0);
            compute.SetBuffer(labelToTexKernel, "LabelBuffer", labelBuffer);
            compute.SetBuffer(labelToTexKernel, "ColorTable", colorTableBuffer);
            compute.SetTexture(labelToTexKernel, "Result", labelTex);

            compute.Dispatch(labelToTexKernel, 256 / 8, 256 / 8, 1);

            return labelTex;
        }

        private static Color32 ToColor(uint c)
        {
            return Color32Extension.FromHex(c);
        }
    }
}
