using System;

namespace Game.LineDraw
{
    /// <summary>
    /// LineHandleをVarStoreにManagedRefとして保存するためのラッパークラス。
    /// structであるLineHandleを参照型として扱えるようにします。
    /// </summary>
    [Serializable]
    public sealed class LineHandleRef
    {
        public LineHandle Handle { get; private set; }

        /// <summary>
        /// このハンドルが属するサービスへの弱参照（optional）。
        /// </summary>
        public WeakReference<ILineDrawService> ServiceRef { get; private set; }

        public LineHandleRef(LineHandle handle, ILineDrawService service = null)
        {
            Handle = handle;
            ServiceRef = service != null ? new WeakReference<ILineDrawService>(service) : null;
        }

        public bool IsValid => Handle.IsValid;

        public bool TryGetService(out ILineDrawService service)
        {
            service = null;
            if (ServiceRef == null)
                return false;
            return ServiceRef.TryGetTarget(out service);
        }

        /// <summary>
        /// ハンドルを更新します（線の再作成時に使用）。
        /// </summary>
        public void UpdateHandle(LineHandle newHandle)
        {
            Handle = newHandle;
        }

        /// <summary>
        /// 無効なハンドル参照を作成します。
        /// </summary>
        public static LineHandleRef Invalid => new LineHandleRef(LineHandle.Invalid);
    }
}
