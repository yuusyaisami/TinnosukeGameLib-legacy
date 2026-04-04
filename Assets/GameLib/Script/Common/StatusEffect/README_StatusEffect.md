# StatusEffect layout and var usage

This folder is split by responsibility.

- Definition: authoring-time definitions and ScriptableObject wrappers
- Runtime: active instance lifecycle, service, state, runtime-only data
- Shared: common DTO/enums used by both Definition and Runtime
- Vars: generated var id constants for Definition/Runtime channels
- MB: MonoBehaviour debug/view helper

## Definition vars vs Runtime vars

Use only these two groups under VarIds.GameLib.Base.StatusEffect:

- Definition.Element.*
  - Data that is fixed by definition assets/presets
  - Example: definitionId, defaultStackMode, defaultRuntimeTag, operations

- Runtime.Element.*
  - Data created/updated after an effect is applied
  - Example: instanceId, stackCount, intensityA..intensityG, remainingDuration, isActive

The same field names can exist in both groups (for example effectType, nameKey, descriptionKey),
but their ownership is different:

- Definition side: source/default value
- Runtime side: live value of the active effect instance

## Write behavior policy

- If only Definition data is provided, write only Definition.Element.*
- If Runtime data is provided (already registered effect), write both:
  - Definition.Element.* (registered definition snapshot)
  - Runtime.Element.* (live runtime snapshot)

## Note about legacy StatusEffect.Element

A legacy flat group may still appear in generated outputs for compatibility/history.
New code should use only Definition.Element or Runtime.Element and avoid the flat Element group.
