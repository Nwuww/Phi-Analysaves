using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace analysaves
{
    public class FittingCoeff
    {
        /// <summary> 难度范围起始值（t=0） </summary>
        public static double DiffMin { get; set; } = 11.0;

        /// <summary> 难度范围结束值（t=1） </summary>
        public static double DiffMax { get; set; } = 17.6;

        /// <summary> 低难度锚点（WLow） </summary>
        public static double DiffLowAnchor { get; set; } = 11.0;

        /// <summary> 高难度锚点（WHigh） </summary>
        public static double DiffHighAnchor { get; set; } = 17.0;

        /// <summary> 在 DiffLowAnchor 处的权重 </summary>
        public static double[] WLow { get; set; } = { 0.7, 0.2, 0.1, 0.0 };

        /// <summary> 在 DiffHighAnchor 处的权重 </summary>
        public static double[] WHigh { get; set; } = { 0.4, 0.1, 0.3, 0.4 };

        /// <summary> 在 DiffMax 处的期望权重 </summary>
        public static double[] WEnd { get; set; } = { 0.3, 0.05, 0.3, 0.35 };

        /// <summary> w3增长曲率 </summary>
        public static double CurveK { get; set; } = 1.2;

        public static List<double> GetWeights(double diff)
        {
            // 1. 计算归一化位置 t
            double t = (diff - DiffMin) / (DiffMax - DiffMin);
            // 截断
            // t = Math.Clamp(t, 0.0, 1.0);

            double tLow = (DiffLowAnchor - DiffMin) / (DiffMax - DiffMin);
            double tHigh = (DiffHighAnchor - DiffMin) / (DiffMax - DiffMin);

            // 2. 计算 w0, w1（分段线性插值）
            double w0, w1;
            double raw2, raw3; // 未归一化的 w2, w3

            if (tLow <= t && t <= tHigh)
            {
                // 低→高段
                double u = (t - tLow) / (tHigh - tLow);
                w0 = WLow[0] + (WHigh[0] - WLow[0]) * u;
                w1 = WLow[1] + (WHigh[1] - WLow[1]) * u;

                // w2 指数插值（保证在 u=0 和 u=1 处匹配）
                raw2 = WLow[2] * Math.Pow(WHigh[2] / WLow[2], u);

                // w3 指数插值（使用 CurveK 控制曲率）
                double expFactor = (Math.Exp(CurveK * u) - 1) / (Math.Exp(CurveK) - 1);
                raw3 = WLow[3] + (WHigh[3] - WLow[3]) * expFactor;
            }
            else if (t > tHigh && t <= 1.0)
            {
                // 高->终点段
                double v = (t - tHigh) / (1.0 - tHigh);
                w0 = WHigh[0] + (WEnd[0] - WHigh[0]) * v;
                w1 = WHigh[1] + (WEnd[1] - WHigh[1]) * v;

                // w2, w3 从 WHigh 指数增长到 WEnd
                raw2 = WHigh[2] * Math.Pow(WEnd[2] / WHigh[2], v);
                raw3 = WHigh[3] * Math.Pow(WEnd[3] / WHigh[3], v);
            }
            else
            {
                // 如果 t 恰好等于边界或超出，提供兜底（实际不会发生）
                w0 = WLow[0]; w1 = WLow[1];
                raw2 = WLow[2]; raw3 = WLow[3];
            }

            // 3. 归一化得到最终 w2, w3
            double s23 = 1 - w0 - w1;
            double sumRaw = raw2 + raw3;
            double w2 = s23 * raw2 / sumRaw;
            double w3 = s23 * raw3 / sumRaw;

            return new List<double> { w0, w1, w2, w3 };
        }

        /// <summary> 偏差的饱和上限（avg占比阈值）
        public static double AvgSaturation { get; set; } = 0.3;

        /// <summary> 特征占比偏差的饱和上限（feat占比阈值）
        public static double CoeffSaturation { get; set; } = 0.3;

        // 用户选择
        public static Func<double, double, double> AvgSmfn = TanhSmooth ?? ((x, _) => x);
        public static Func<double, double, double> CoeffSmfn = TanhSmooth ?? ((x, _) => x);

        // tanh平滑函数
        public static readonly Func<double, double, double> TanhSmooth = (x, saturation) => saturation * Math.Tanh(x / saturation);
        
        // 双极性sigmoid函数 (似乎就是灵活度更高的tanh？无所谓了ww)
        public static readonly Func<double, double, double> BipolarSigmoid = (x, saturation)
            => saturation * (2.0 * (1.0 / (1.0 + Math.Exp(-2.0 * x / saturation))) - 1.0);

        // 我超 突然发现其实平滑的原理和梯度下降算法神似，试试其他几个
        // pseudo-huber loss 放大微小差距 适合高定数
        public static readonly Func<double, double, double> PseudoHuber = (x, saturation)
            => (x>=0 ? 1 : -1) * Math.Pow(saturation, 2) * (Math.Sqrt(1.0 + Math.Pow(x / saturation, 2)) - 1.0);

        // 逐特征自适应归一化 AdaGrad
        // 每个特征独有的饱和系数
        public static double[] FeatSaturationScales { get; set; } = { 0.3, 0.2, 0.1 };
        public static double SmoothCoeff_Adaptive(double relativeDev, int featIndex)
            => TanhSmooth(relativeDev, FeatSaturationScales[featIndex]);


        // <summary> 平滑化平均偏差 </summary>
        public static double SmoothAvg(double avgDeviation)
        {
            return AvgSmfn(avgDeviation, AvgSaturation);
        }

        // <summary> 平滑化特征占比偏差 </summary>
        public static double SmoothCoeff(double relativeDeviation)
        {
            return CoeffSmfn(relativeDeviation, CoeffSaturation);
        }
    }
}
