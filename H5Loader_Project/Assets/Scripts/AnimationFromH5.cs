﻿/*using System;
using System.CodeDom;
using System.Text.RegularExpressions;
using HDF.PInvoke;
using MoshPlayer.Scripts.SMPLModel;
using MoshPlayer.Scripts.Utilities;
using UnityEngine;



namespace MoshPlayer.Scripts.FileLoaders {
    public sealed class AnimationFromH5 : AnimationLoadStrategy {
        const int MaxBodyShapeBetaCount = 100;
        const int MaxStringLengthForReading = 20;

        public AnimationFromH5(string filePath, Models possibleModels) : base(filePath, possibleModels) { }
   

        protected override bool IsMatchingModel(ModelDefinition model) {
            long fileId = H5F.open(filePath, H5F.ACC_RDONLY);
            double[] testBetas;
            try {
                OpenedH5DataSet betasDataset = new OpenedH5DataSet(fileId, model.H5Keys.Betas);
                testBetas = betasDataset.LoadAs<double>(betasDataset.Dimensions[0]);
            }
            finally {
                H5F.close(fileId);
            }
            //Debug.Log($"betas.Length {testBetas.Length}, model shapecount: {model.BodyShapeBetaCount}, modelname: {model.name}");
            return testBetas.Length == model.BodyShapeBetaCount;
        }


        protected override void FormatData() {
            long fileId = H5F.open(filePath, H5F.ACC_RDONLY);
            try {
                Data.Betas = ReadBetas(fileId);
                Data.Gender = ReadGender(fileId);
                Data.Fps = ReadFPS(fileId);
                LoadTranslationAndPosesFromJoints(fileId);
            }
            finally {
                H5F.close(fileId);
            }
            
           //Data.ShowDebug();
        }

        float[] ReadBetas(long fileId) {
            OpenedH5DataSet betasDataset = new OpenedH5DataSet(fileId, Data.Model.H5Keys.Betas);
        
            double[] betaArray = betasDataset.LoadAs<double>(Data.Model.BodyShapeBetaCount);

            float[] finalBetas = ToFloatArray(betaArray);
            
            return finalBetas;
        }

        Gender ReadGender(long fileId) {
            OpenedH5DataSet genderDataSet = new OpenedH5DataSet(fileId, Data.Model.H5Keys.Gender);
        
            
            
            
            if (replacedString == Data.Model.H5Keys.Male) return Gender.Male;
            else if (replacedString == Data.Model.H5Keys.Female) return Gender.Female;
            else throw new DataReadException($"Unexpected value for gender ({replacedString}) in file: {filePath}");
        }

        void LoadTranslationAndPosesFromJoints(long fileId) {
            OpenedH5DataSet translationDataset = new OpenedH5DataSet(fileId, Data.Model.H5Keys.Translations);
            OpenedH5DataSet posesDataset = new OpenedH5DataSet(fileId, Data.Model.H5Keys.Poses);
            
            Data.FrameCount = translationDataset.Dimensions[0];


            double[] translationVectorizedDoubles = translationDataset.LoadAs<double>(Data.FrameCount*3);
            double[] posesVectorizedDoubles = posesDataset.LoadAs<double>(Data.FrameCount * Data.Model.JointCount * 3);
            
            float[] translationVectorized = ToFloatArray(translationVectorizedDoubles);
            float[] posesVectorized = ToFloatArray(posesVectorizedDoubles);
            
            
            Data.Translations = new Vector3[Data.FrameCount];
            Data.Poses = new Quaternion[Data.FrameCount, Data.Model.JointCount];
            for (int frameIndex = 0; frameIndex < Data.FrameCount; frameIndex++) {
                GetTranslationAtFrame(frameIndex, translationVectorized);
                for (int jointIndex = 0; jointIndex < Data.Model.JointCount; jointIndex++) {
                    GetPosesAtFrame(frameIndex, jointIndex, posesVectorized);
                }
            }
        }

        void GetPosesAtFrame(int frameIndex, int jointIndex, float[] posesVectorized) {
            int vectorIndex = frameIndex * Data.Model.JointCount * 3 + jointIndex * 3;
            var rx = posesVectorized[vectorIndex];
            var ry = posesVectorized[vectorIndex + 1];
            var rz = posesVectorized[vectorIndex + 2];
            Vector3 rotationVectorInMayaCoords = new Vector3(rx, ry, rz);
            Quaternion quaternionInMayaCoords = RotationVectorToQuaternion(rotationVectorInMayaCoords);
            //Debug.Log($"Frame: {frameIndex}, Joint: {jointIndex}: rotvec: {rotationVectorInMayaCoords.ToString("F4")} quat: {quaternionInMayaCoords.ToString("F4")}");
            Quaternion quaternionInUnityCoords = quaternionInMayaCoords.ToLeftHanded();
            
            Data.Poses[frameIndex, jointIndex] = quaternionInUnityCoords;
        }

        /// <summary>
        /// Adapted from https://github.com/facebookresearch/QuaterNet/blob/master/common/quaternion.py [expmap_to_quaternion()]
        /// With help from Saeed Ghorbani
        /// </summary>
        /// <param name="rotationVector"></param>
        /// <returns></returns>
        Quaternion RotationVectorToQuaternion(Vector3 rotationVector) {

            //Important to avoid dividing by zero errors due to sinC
            if (rotationVector == Vector3.zero) return Quaternion.identity;
            
            float theta = rotationVector.magnitude;
            
            var qx = 0.5f * SinC(0.5f * (theta/Mathf.PI))*rotationVector.x;
            var qy = 0.5f * SinC(0.5f * (theta/Mathf.PI))*rotationVector.y;
            var qz = 0.5f * SinC(0.5f * (theta/Mathf.PI))*rotationVector.z;
            var qw = Mathf.Cos(0.5f * theta);
            Quaternion quat = new Quaternion(qx, qy, qz, qw);
            
            //Debug.Log($"quat {quat.ToString("F4")} rotationVector: {rotationVector.ToString("F2")}");
            return quat;
        }

        float SinC(float x) {
            float result = Mathf.Sin(Mathf.PI * x) / (Mathf.PI * x);
            return result;
        }

        void GetTranslationAtFrame(int frameIndex, float[] translationVectorizedFloats) {
            int vectorIndex = frameIndex * 3;
            var x = translationVectorizedFloats[vectorIndex];
            var y = translationVectorizedFloats[vectorIndex + 1];
            var z = translationVectorizedFloats[vectorIndex + 2];
            Vector3 translationInMayaCoords = new Vector3(x, y, z);
            Vector3 translationInUnityCoords = translationInMayaCoords.ConvertTranslationFromMayaToUnity();
            Data.Translations[frameIndex] = translationInUnityCoords;
        }

        int ReadFPS(long fileId) {
            OpenedH5DataSet fpsDataset = new OpenedH5DataSet(fileId, Data.Model.H5Keys.FPS);

            double[] fpsArray = fpsDataset.LoadAs<double>(1);

            int loadedFPS = (int)Math.Round(fpsArray[0]);
            return loadedFPS;
        }

        static float[] ToFloatArray(double[] betaArray) {
            float[] floatArr = new float[betaArray.Length];
            for (int index = 0; index < betaArray.Length; index++) {
                floatArr[index] = (float) betaArray[index];
            }

            return floatArr;
        }
    }
}*/