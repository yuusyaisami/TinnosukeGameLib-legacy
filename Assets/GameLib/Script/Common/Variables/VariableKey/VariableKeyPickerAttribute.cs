using System;
using UnityEngine;

/// <summary>Marks a string field to use the Variable Key picker window.</summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class VariableKeyPickerAttribute : PropertyAttribute
{
    public string RegistryFieldName { get; }

    public VariableKeyPickerAttribute(string registryFieldName = null)
    {
        RegistryFieldName = registryFieldName;
    }
}
