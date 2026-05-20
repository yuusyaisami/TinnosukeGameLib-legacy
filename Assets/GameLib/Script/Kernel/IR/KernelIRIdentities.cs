#nullable enable
using System;

namespace Game.Kernel.IR
{
    public readonly struct ModuleId : IEquatable<ModuleId>
    {
        public ModuleId(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public bool Equals(ModuleId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object? obj)
        {
            return obj is ModuleId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return "ModuleId(" + Value + ")";
        }

        public static bool operator ==(ModuleId left, ModuleId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ModuleId left, ModuleId right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct ServiceId : IEquatable<ServiceId>
    {
        public ServiceId(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public bool Equals(ServiceId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object? obj)
        {
            return obj is ServiceId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return "ServiceId(" + Value + ")";
        }

        public static bool operator ==(ServiceId left, ServiceId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ServiceId left, ServiceId right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct ScopeAuthoringId : IEquatable<ScopeAuthoringId>
    {
        public ScopeAuthoringId(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public bool Equals(ScopeAuthoringId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object? obj)
        {
            return obj is ScopeAuthoringId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return "ScopeAuthoringId(" + Value + ")";
        }

        public static bool operator ==(ScopeAuthoringId left, ScopeAuthoringId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ScopeAuthoringId left, ScopeAuthoringId right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct ScopePlanId : IEquatable<ScopePlanId>
    {
        public ScopePlanId(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public bool Equals(ScopePlanId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object? obj)
        {
            return obj is ScopePlanId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return "ScopePlanId(" + Value + ")";
        }

        public static bool operator ==(ScopePlanId left, ScopePlanId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ScopePlanId left, ScopePlanId right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct CommandTypeId : IEquatable<CommandTypeId>
    {
        public CommandTypeId(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public bool Equals(CommandTypeId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object? obj)
        {
            return obj is CommandTypeId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return "CommandTypeId(" + Value + ")";
        }

        public static bool operator ==(CommandTypeId left, CommandTypeId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CommandTypeId left, CommandTypeId right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct CommandExecutorId : IEquatable<CommandExecutorId>
    {
        public CommandExecutorId(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public bool Equals(CommandExecutorId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object? obj)
        {
            return obj is CommandExecutorId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return "CommandExecutorId(" + Value + ")";
        }

        public static bool operator ==(CommandExecutorId left, CommandExecutorId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CommandExecutorId left, CommandExecutorId right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct CommandPayloadSchemaId : IEquatable<CommandPayloadSchemaId>
    {
        public CommandPayloadSchemaId(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public bool Equals(CommandPayloadSchemaId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object? obj)
        {
            return obj is CommandPayloadSchemaId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return "CommandPayloadSchemaId(" + Value + ")";
        }

        public static bool operator ==(CommandPayloadSchemaId left, CommandPayloadSchemaId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CommandPayloadSchemaId left, CommandPayloadSchemaId right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct CommandAuthoringKeyId : IEquatable<CommandAuthoringKeyId>
    {
        public CommandAuthoringKeyId(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public bool Equals(CommandAuthoringKeyId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object? obj)
        {
            return obj is CommandAuthoringKeyId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return "CommandAuthoringKeyId(" + Value + ")";
        }

        public static bool operator ==(CommandAuthoringKeyId left, CommandAuthoringKeyId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CommandAuthoringKeyId left, CommandAuthoringKeyId right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct ValueKeyId : IEquatable<ValueKeyId>
    {
        public ValueKeyId(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public bool Equals(ValueKeyId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object? obj)
        {
            return obj is ValueKeyId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return "ValueKeyId(" + Value + ")";
        }

        public static bool operator ==(ValueKeyId left, ValueKeyId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ValueKeyId left, ValueKeyId right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct ValueSchemaId : IEquatable<ValueSchemaId>
    {
        public ValueSchemaId(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public bool Equals(ValueSchemaId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object? obj)
        {
            return obj is ValueSchemaId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return "ValueSchemaId(" + Value + ")";
        }

        public static bool operator ==(ValueSchemaId left, ValueSchemaId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ValueSchemaId left, ValueSchemaId right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct TableId : IEquatable<TableId>
    {
        public TableId(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public bool Equals(TableId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object? obj)
        {
            return obj is TableId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return "TableId(" + Value + ")";
        }

        public static bool operator ==(TableId left, TableId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TableId left, TableId right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct TableRowId : IEquatable<TableRowId>
    {
        public TableRowId(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public bool Equals(TableRowId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object? obj)
        {
            return obj is TableRowId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return "TableRowId(" + Value + ")";
        }

        public static bool operator ==(TableRowId left, TableRowId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TableRowId left, TableRowId right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct TableColumnId : IEquatable<TableColumnId>
    {
        public TableColumnId(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public bool Equals(TableColumnId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object? obj)
        {
            return obj is TableColumnId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return "TableColumnId(" + Value + ")";
        }

        public static bool operator ==(TableColumnId left, TableColumnId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TableColumnId left, TableColumnId right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct TableCellId : IEquatable<TableCellId>
    {
        public TableCellId(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public bool Equals(TableCellId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object? obj)
        {
            return obj is TableCellId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return "TableCellId(" + Value + ")";
        }

        public static bool operator ==(TableCellId left, TableCellId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TableCellId left, TableCellId right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct RecordId : IEquatable<RecordId>
    {
        public RecordId(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public bool Equals(RecordId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object? obj)
        {
            return obj is RecordId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return "RecordId(" + Value + ")";
        }

        public static bool operator ==(RecordId left, RecordId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(RecordId left, RecordId right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct RecordFieldId : IEquatable<RecordFieldId>
    {
        public RecordFieldId(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public bool Equals(RecordFieldId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object? obj)
        {
            return obj is RecordFieldId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return "RecordFieldId(" + Value + ")";
        }

        public static bool operator ==(RecordFieldId left, RecordFieldId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(RecordFieldId left, RecordFieldId right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct RecordListId : IEquatable<RecordListId>
    {
        public RecordListId(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public bool Equals(RecordListId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object? obj)
        {
            return obj is RecordListId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return "RecordListId(" + Value + ")";
        }

        public static bool operator ==(RecordListId left, RecordListId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(RecordListId left, RecordListId right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct RecordListElementId : IEquatable<RecordListElementId>
    {
        public RecordListElementId(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public bool Equals(RecordListElementId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object? obj)
        {
            return obj is RecordListElementId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return "RecordListElementId(" + Value + ")";
        }

        public static bool operator ==(RecordListElementId left, RecordListElementId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(RecordListElementId left, RecordListElementId right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct LifecycleStepId : IEquatable<LifecycleStepId>
    {
        public LifecycleStepId(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public bool Equals(LifecycleStepId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object? obj)
        {
            return obj is LifecycleStepId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return "LifecycleStepId(" + Value + ")";
        }

        public static bool operator ==(LifecycleStepId left, LifecycleStepId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(LifecycleStepId left, LifecycleStepId right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct RuntimeQueryId : IEquatable<RuntimeQueryId>
    {
        public RuntimeQueryId(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public bool Equals(RuntimeQueryId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object? obj)
        {
            return obj is RuntimeQueryId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return "RuntimeQueryId(" + Value + ")";
        }

        public static bool operator ==(RuntimeQueryId left, RuntimeQueryId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(RuntimeQueryId left, RuntimeQueryId right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct SourceLocationId : IEquatable<SourceLocationId>
    {
        public SourceLocationId(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public bool Equals(SourceLocationId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object? obj)
        {
            return obj is SourceLocationId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return "SourceLocationId(" + Value + ")";
        }

        public static bool operator ==(SourceLocationId left, SourceLocationId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SourceLocationId left, SourceLocationId right)
        {
            return !left.Equals(right);
        }
    }
}