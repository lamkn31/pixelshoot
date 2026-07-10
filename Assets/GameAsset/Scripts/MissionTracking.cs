using System;
using System.Collections.Generic;

namespace Wayfu.Lamkn
{
    /// <summary>
    /// Bridge cho mission analytics. Code gameplay (các asmdef) KHÔNG tham chiếu ngược được
    /// vào Assembly-CSharp nơi đặt CrazyLabsTracking, nên gameplay gọi qua lớp này. Phần SDK
    /// (CrazyLabsInit, trong Assembly-CSharp) gán các handler tới CrazyLabsTracking lúc init.
    /// Nếu chưa gán (ví dụ chạy trong editor không có SDK) thì các lệnh gọi là no-op.
    /// </summary>
    public static class MissionTracking
    {
        public static Action<Dictionary<string, object>> StartedHandler;
        public static Action<Dictionary<string, object>> CompletedHandler;
        public static Action<Dictionary<string, object>> FailedHandler;

        public static void MissionStarted(Dictionary<string, object> parameters = null)
            => StartedHandler?.Invoke(parameters);

        public static void MissionCompleted(Dictionary<string, object> parameters = null)
            => CompletedHandler?.Invoke(parameters);

        public static void MissionFailed(Dictionary<string, object> parameters = null)
            => FailedHandler?.Invoke(parameters);
    }
}
