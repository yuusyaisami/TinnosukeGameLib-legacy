#nullable enable
using System;
using Game.Common;
using Game.Trait;

namespace Game.Item
{
    public interface IItemDefinition : ITraitDefinition
    {
        string ItemId { get; }
        IItemInstance CreateInstance(ItemInstanceContext context);
    }

    public interface IItemInstance : ITraitInstance
    {
        new IItemDefinition Definition { get; }
        new ItemInstanceContext Context { get; }
    }

    public sealed class ItemInstanceContext : TraitInstanceContext
    {
        public ItemInstanceContext(IScopeNode? scope)
            : base(scope)
        {
        }
    }

    public abstract class ItemDefinitionSO : TraitDefinitionSO, IItemDefinition
    {
        public string ItemId => DefinitionId;
        public override string RefKeyPrefix => "item";

        public IItemInstance CreateInstance(ItemInstanceContext context)
        {
            ApplyCommonVars(context);
            return new ItemDefinitionInstance(this, context);
        }

        public override ITraitInstance CreateInstance(TraitInstanceContext context)
        {
            if (context is ItemInstanceContext itemContext)
                return CreateInstance(itemContext);

            return CreateInstance(new ItemInstanceContext(context.Scope));
        }

        sealed class ItemDefinitionInstance : IItemInstance
        {
            readonly ItemDefinitionSO _definition;
            readonly string _instanceId = Guid.NewGuid().ToString("N");

            public string InstanceId => _instanceId;
            public IItemDefinition Definition => _definition;
            ITraitDefinition ITraitInstance.Definition => _definition;
            public ItemInstanceContext Context { get; }
            TraitInstanceContext ITraitInstance.Context => Context;

            public ItemDefinitionInstance(ItemDefinitionSO definition, ItemInstanceContext context)
            {
                _definition = definition;
                Context = context;
            }

            public void OnHold()
            {
                _definition.OnHold(this);
            }

            public void OnLtsInstantiated(IScopeNode scope)
            {
                _definition.OnLtsInstantiated(this, scope);
            }

            public void OnAdded()
            {
                _definition.OnAdded(this);
            }

            public void OnUse()
            {
                _definition.OnUse(this);
            }

            public void OnRemove()
            {
                _definition.OnRemove(this);
            }
        }
    }
}
