#nullable enable
using System;
using Game.Common;
using Game;
using Game.Commands.VNext;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Channel
{
    public enum TypewriterEventCommandHook
    {
        TypewriterStart = 10,
        TextShowed = 20,
        TextDisappeared = 30,
        CharacterVisible = 40,
        CharacterWaitStarted = 50,
        CharacterWaitFinished = 60,
        Message = 70,
    }

    [Serializable]
    public sealed class TypewriterEventCommandBindings
    {
        [LabelText("Apply Typewriter Events")]
        public bool Apply;

        [ShowIf(nameof(Apply))]
        [LabelText("On Typewriter Start")]
        public CommandListData OnTypewriterStart = new();

        [ShowIf(nameof(Apply))]
        [LabelText("On Text Showed")]
        public CommandListData OnTextShowed = new();

        [ShowIf(nameof(Apply))]
        [LabelText("On Text Disappeared")]
        public CommandListData OnTextDisappeared = new();

        [ShowIf(nameof(Apply))]
        [LabelText("On Character Visible")]
        public CommandListData OnCharacterVisible = new();

        [ShowIf(nameof(Apply))]
        [LabelText("On Character Wait Started")]
        public CommandListData OnCharacterWaitStarted = new();

        [ShowIf(nameof(Apply))]
        [LabelText("On Character Wait Finished")]
        public CommandListData OnCharacterWaitFinished = new();

        [ShowIf(nameof(Apply))]
        [LabelText("On Message")]
        public CommandListData OnMessage = new();

        public CommandListData? Resolve(TypewriterEventCommandHook hook)
        {
            return hook switch
            {
                TypewriterEventCommandHook.TypewriterStart => OnTypewriterStart,
                TypewriterEventCommandHook.TextShowed => OnTextShowed,
                TypewriterEventCommandHook.TextDisappeared => OnTextDisappeared,
                TypewriterEventCommandHook.CharacterVisible => OnCharacterVisible,
                TypewriterEventCommandHook.CharacterWaitStarted => OnCharacterWaitStarted,
                TypewriterEventCommandHook.CharacterWaitFinished => OnCharacterWaitFinished,
                TypewriterEventCommandHook.Message => OnMessage,
                _ => null,
            };
        }

        public bool HasAnyCommands()
        {
            if (!Apply)
                return false;

            return HasCommands(OnTypewriterStart) ||
                   HasCommands(OnTextShowed) ||
                   HasCommands(OnTextDisappeared) ||
                   HasCommands(OnCharacterVisible) ||
                   HasCommands(OnCharacterWaitStarted) ||
                   HasCommands(OnCharacterWaitFinished) ||
                   HasCommands(OnMessage);
        }

        public void BindDebugOwner(UnityEngine.Object owner, string rootPath)
        {
            OnTypewriterStart?.BindDebugOwner(owner, $"{rootPath}.{nameof(OnTypewriterStart)}");
            OnTextShowed?.BindDebugOwner(owner, $"{rootPath}.{nameof(OnTextShowed)}");
            OnTextDisappeared?.BindDebugOwner(owner, $"{rootPath}.{nameof(OnTextDisappeared)}");
            OnCharacterVisible?.BindDebugOwner(owner, $"{rootPath}.{nameof(OnCharacterVisible)}");
            OnCharacterWaitStarted?.BindDebugOwner(owner, $"{rootPath}.{nameof(OnCharacterWaitStarted)}");
            OnCharacterWaitFinished?.BindDebugOwner(owner, $"{rootPath}.{nameof(OnCharacterWaitFinished)}");
            OnMessage?.BindDebugOwner(owner, $"{rootPath}.{nameof(OnMessage)}");
        }

        static bool HasCommands(CommandListData? list) => list != null && list.Count > 0;
    }

    [Serializable]
    public sealed class TypewriterEventCommandMutationEntry
    {
        [LabelText("Apply")]
        public bool Apply;

        [ShowIf(nameof(Apply))]
        [LabelText("Operation")]
        public CommandListMutationOperation Operation = CommandListMutationOperation.Append;

        [ShowIf(nameof(ShouldShowCommands))]
        [LabelText("Commands")]
        [CommandListFunctionName("TypewriterEvent.Mutation.Commands")]
        public CommandListData Commands = new();

        bool ShouldShowCommands()
        {
            if (!Apply)
                return false;

            return Operation == CommandListMutationOperation.Append ||
                   Operation == CommandListMutationOperation.Override;
        }
    }

    [Serializable]
    public sealed class TypewriterEventCommandMutations
    {
        [LabelText("On Typewriter Start")]
        public TypewriterEventCommandMutationEntry OnTypewriterStart = new();

        [LabelText("On Text Showed")]
        public TypewriterEventCommandMutationEntry OnTextShowed = new();

        [LabelText("On Text Disappeared")]
        public TypewriterEventCommandMutationEntry OnTextDisappeared = new();

        [LabelText("On Character Visible")]
        public TypewriterEventCommandMutationEntry OnCharacterVisible = new();

        [LabelText("On Character Wait Started")]
        public TypewriterEventCommandMutationEntry OnCharacterWaitStarted = new();

        [LabelText("On Character Wait Finished")]
        public TypewriterEventCommandMutationEntry OnCharacterWaitFinished = new();

        [LabelText("On Message")]
        public TypewriterEventCommandMutationEntry OnMessage = new();

        public TypewriterEventCommandMutationEntry? Resolve(TypewriterEventCommandHook hook)
        {
            return hook switch
            {
                TypewriterEventCommandHook.TypewriterStart => OnTypewriterStart,
                TypewriterEventCommandHook.TextShowed => OnTextShowed,
                TypewriterEventCommandHook.TextDisappeared => OnTextDisappeared,
                TypewriterEventCommandHook.CharacterVisible => OnCharacterVisible,
                TypewriterEventCommandHook.CharacterWaitStarted => OnCharacterWaitStarted,
                TypewriterEventCommandHook.CharacterWaitFinished => OnCharacterWaitFinished,
                TypewriterEventCommandHook.Message => OnMessage,
                _ => null,
            };
        }
    }

    public sealed class TypewriterEventCommandRuntimeConfig
    {
        public CommandListData? OnTypewriterStart;
        public CommandListData? OnTextShowed;
        public CommandListData? OnTextDisappeared;
        public CommandListData? OnCharacterVisible;
        public CommandListData? OnCharacterWaitStarted;
        public CommandListData? OnCharacterWaitFinished;
        public CommandListData? OnMessage;

        public bool HasAnyCommands()
        {
            return HasCommands(OnTypewriterStart) ||
                   HasCommands(OnTextShowed) ||
                   HasCommands(OnTextDisappeared) ||
                   HasCommands(OnCharacterVisible) ||
                   HasCommands(OnCharacterWaitStarted) ||
                   HasCommands(OnCharacterWaitFinished) ||
                   HasCommands(OnMessage);
        }

        public CommandListData? Resolve(TypewriterEventCommandHook hook)
        {
            return hook switch
            {
                TypewriterEventCommandHook.TypewriterStart => OnTypewriterStart,
                TypewriterEventCommandHook.TextShowed => OnTextShowed,
                TypewriterEventCommandHook.TextDisappeared => OnTextDisappeared,
                TypewriterEventCommandHook.CharacterVisible => OnCharacterVisible,
                TypewriterEventCommandHook.CharacterWaitStarted => OnCharacterWaitStarted,
                TypewriterEventCommandHook.CharacterWaitFinished => OnCharacterWaitFinished,
                TypewriterEventCommandHook.Message => OnMessage,
                _ => null,
            };
        }

        static bool HasCommands(CommandListData? list) => list != null && list.Count > 0;
    }

    public readonly struct TypewriterEventCommandRuntimeContext
    {
        public readonly IScopeNode Scope;
        public readonly ICommandRunner Runner;
        public readonly IVarStore Vars;
        public readonly IScopeNode? Actor;
        public readonly IScopeNode? CommandRootScope;
        public readonly IScopeNode? RootActor;
        public readonly IScopeNode? CallerActor;
        public readonly CommandRunOptions Options;
        public readonly CommandContext SourceContext;

        public TypewriterEventCommandRuntimeContext(CommandContext ctx)
        {
            Scope = ctx.Scope;
            Runner = ctx.Runner;
            Vars = ctx.Vars;
            Actor = ctx.Actor;
            CommandRootScope = ctx.CommandRootScope;
            RootActor = ctx.RootActor;
            CallerActor = ctx.CallerActor;
            Options = ctx.Options;
            SourceContext = ctx;
        }

        public CommandContext CreateCommandContext()
        {
            return new CommandContext(
                Scope,
                Vars,
                Runner,
                Actor,
                Options,
                CommandRootScope,
                RootActor,
                CallerActor,
                SourceContext);
        }
    }
}
