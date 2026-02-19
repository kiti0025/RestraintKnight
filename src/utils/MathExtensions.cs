using Godot;
using System;

namespace RestraintKnight.Utils
{
    /// <summary>
    /// 数学扩展方法类，补充Godot缺失的SmoothDamp方法，行为完全对齐Unity
    /// </summary>
    public static class MathExtensions
    {
        #region Float 平滑阻尼实现
        /// <summary>
        /// 浮点型平滑阻尼，与Unity Mathf.SmoothDamp行为完全一致
        /// </summary>
        /// <param name="current">当前值</param>
        /// <param name="target">目标值</param>
        /// <param name="currentVelocity">当前速度（引用传递，方法内自动更新）</param>
        /// <param name="smoothTime">平滑时间，越小到达目标越快</param>
        /// <param name="maxSpeed">最大速度限制</param>
        /// <param name="deltaTime">帧间隔时间</param>
        /// <returns>平滑后的当前值</returns>
        public static float SmoothDamp(this float current, float target, ref float currentVelocity, float smoothTime, float maxSpeed = float.MaxValue, float deltaTime = 0.016f)
        {
            smoothTime = Mathf.Max(0.0001f, smoothTime);
            float omega = 2f / smoothTime;

            float x = omega * deltaTime;
            float exp = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);

            float change = current - target;
            float originalTo = target;

            // 限制最大速度
            float maxChange = maxSpeed * smoothTime;
            change = Mathf.Clamp(change, -maxChange, maxChange);
            target = current - change;

            float temp = (currentVelocity + omega * change) * deltaTime;
            currentVelocity = (currentVelocity - omega * temp) * exp;
            float result = target + (change + temp) * exp;

            // 防止数值过冲
            if ((originalTo - current > 0f) == (result > originalTo))
            {
                result = originalTo;
                currentVelocity = (result - originalTo) / deltaTime;
            }

            return result;
        }
        #endregion

        #region Vector2 平滑阻尼实现
        /// <summary>
        /// Vector2型平滑阻尼，与Unity Vector2.SmoothDamp行为完全一致
        /// </summary>
        /// <param name="current">当前向量</param>
        /// <param name="target">目标向量</param>
        /// <param name="currentVelocity">当前速度向量（引用传递，方法内自动更新）</param>
        /// <param name="smoothTime">平滑时间，越小到达目标越快</param>
        /// <param name="maxSpeed">最大速度限制</param>
        /// <param name="deltaTime">帧间隔时间</param>
        /// <returns>平滑后的当前向量</returns>
        public static Vector2 SmoothDamp(this Vector2 current, Vector2 target, ref Vector2 currentVelocity, float smoothTime, float maxSpeed = float.MaxValue, float deltaTime = 0.016f)
        {
            float x = current.X.SmoothDamp(target.X, ref currentVelocity.X, smoothTime, maxSpeed, deltaTime);
            float y = current.Y.SmoothDamp(target.Y, ref currentVelocity.Y, smoothTime, maxSpeed, deltaTime);
            return new Vector2(x, y);
        }
        #endregion
    }
}