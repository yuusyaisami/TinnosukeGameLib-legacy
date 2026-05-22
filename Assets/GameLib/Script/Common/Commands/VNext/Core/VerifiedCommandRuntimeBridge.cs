#nullable enable
using System;
using System.Collections.Generic;

namespace Game.Commands.VNext
{
    public readonly struct ExplicitCommandExecutorBinding
    {
        public ExplicitCommandExecutorBinding(Type executorType)
        {
            if (executorType == null)
                throw new ArgumentNullException(nameof(executorType));

            if (!typeof(ICommandExecutor).IsAssignableFrom(executorType))
                throw new ArgumentException("Explicit command executor bindings must target ICommandExecutor types.", nameof(executorType));

            ExecutorType = executorType;
        }

        public Type ExecutorType { get; }

        public static ExplicitCommandExecutorBinding For<TExecutor>() where TExecutor : class, ICommandExecutor
        {
            return new ExplicitCommandExecutorBinding(typeof(TExecutor));
        }
    }

    public interface IVerifiedCommandRuntimeSession
    {
        ICommandCatalog Catalog { get; }

        ICommandKeyResolver KeyResolver { get; }

        ICommandPayloadReferenceValidator PayloadReferenceValidator { get; }

        ICommandExecutorCatalog CreateExecutorCatalog(IRuntimeResolver resolver, IReadOnlyList<ExplicitCommandExecutorBinding> bindings);
    }

    public static class VerifiedCommandRuntimeBridge
    {
        static IVerifiedCommandRuntimeSession? s_session;

        public static bool IsActive => s_session != null;

        public static void Activate(IVerifiedCommandRuntimeSession session)
        {
            s_session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public static void Deactivate()
        {
            s_session = null;
        }

        public static bool TryGetSession(out IVerifiedCommandRuntimeSession? session)
        {
            session = s_session;
            return session != null;
        }
    }
}