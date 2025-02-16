using System;
using System.Collections.Generic;
using System.Linq;
using HotChocolate.Configuration;
using HotChocolate.Data.Filters;
using HotChocolate.Internal;
using HotChocolate.Types;
using HotChocolate.Types.Descriptors;
using HotChocolate.Types.Descriptors.Definitions;
using static HotChocolate.Data.ThrowHelper;

namespace HotChocolate.Data.Sorting;

public sealed class SortTypeInterceptor : TypeInterceptor
{
    private readonly Dictionary<string, ISortConvention> _conventions = new();
    private readonly List<Func<ITypeReference>> _typesToRegister = new();
    private TypeRegistry _typeRegistry = default!;
    private readonly Dictionary<ITypeSystemMember, SortInputTypeDefinition> _definitions = new();

    internal override void InitializeContext(
        IDescriptorContext context,
        TypeInitializer typeInitializer,
        TypeRegistry typeRegistry,
        TypeLookup typeLookup,
        TypeReferenceResolver typeReferenceResolver)
    {
        _typeRegistry = typeRegistry;
    }

    public override void OnBeforeRegisterDependencies(
        ITypeDiscoveryContext discoveryContext,
        DefinitionBase definition)
    {
        switch (definition)
        {
            case SortInputTypeDefinition inputDefinition:
                OnBeforeRegisteringDependencies(discoveryContext, inputDefinition);
                break;
            case SortEnumTypeDefinition enumTypeDefinition:
                OnBeforeRegisteringDependencies(discoveryContext, enumTypeDefinition);
                break;
        }
    }

    public override void OnBeforeCompleteName(
        ITypeCompletionContext completionContext,
        DefinitionBase definition)
    {
        switch (definition)
        {
            case SortInputTypeDefinition inputDefinition:
                OnBeforeCompleteName(completionContext, inputDefinition);
                break;
            case SortEnumTypeDefinition enumTypeDefinition:
                OnBeforeCompleteName(completionContext, enumTypeDefinition);
                break;
        }
    }

    public override void OnBeforeCompleteType(
        ITypeCompletionContext completionContext,
        DefinitionBase definition)
    {
        switch (definition)
        {
            case SortInputTypeDefinition inputDefinition:
                OnBeforeCompleteType(completionContext, inputDefinition);
                break;
            case SortEnumTypeDefinition enumTypeDefinition:
                OnBeforeCompleteType(completionContext, enumTypeDefinition);
                break;
        }
    }

    private void OnBeforeRegisteringDependencies(
        ITypeDiscoveryContext discoveryContext,
        SortInputTypeDefinition definition)
    {
        var convention =
            GetConvention(discoveryContext.DescriptorContext, definition.Scope);

        _definitions[discoveryContext.Type] = definition;

        var descriptor = SortInputTypeDescriptor.New(
            discoveryContext.DescriptorContext,
            definition.EntityType!,
            definition.Scope);

        var typeReference =
            TypeReference.Create( discoveryContext.Type, definition.Scope);

        convention.ApplyConfigurations(typeReference, descriptor);

        var extensionDefinition = descriptor.CreateDefinition();

        discoveryContext.RegisterDependencies(extensionDefinition);

        foreach (var field in definition.Fields)
        {
            if (field is SortFieldDefinition sortField)
            {
                RegisterDynamicTypeConfiguration(
                    discoveryContext,
                    typeReference,
                    definition,
                    sortField);
            }
        }
    }

    private void OnBeforeRegisteringDependencies(
        ITypeDiscoveryContext discoveryContext,
        SortEnumTypeDefinition definition)
    {
        var convention =
            GetConvention(discoveryContext.DescriptorContext, definition.Scope);

        var descriptor = SortEnumTypeDescriptor.New(
            discoveryContext.DescriptorContext,
            definition.EntityType,
            definition.Scope);

        var typeReference =
            TypeReference.Create(discoveryContext.Type, definition.Scope);

        convention.ApplyConfigurations(typeReference, descriptor);

        var extensionDefinition = descriptor.CreateDefinition();

        discoveryContext.RegisterDependencies(extensionDefinition);
    }

    private void RegisterDynamicTypeConfiguration(
        ITypeDiscoveryContext discoveryContext,
        ITypeReference typeReference,
        SortInputTypeDefinition parentTypeDefinition,
        SortFieldDefinition sortField)
    {
        if (sortField.CreateFieldTypeDefinition is null)
        {
            return;
        }

        ITypeReference? originalType;
        _typesToRegister.Add(() =>
        {
            originalType = sortField.Type;
            sortField.Type = TypeReference.Create(
                $"SortSubTypeConfiguration_{Guid.NewGuid():N}",
                typeReference,
                Factory,
                TypeContext.Input);

            return sortField.Type;

            TypeSystemObjectBase Factory(IDescriptorContext _)
            {
                SortInputTypeDefinition? explicitDefinition = null;

                if (sortField.CreateFieldTypeDefinition is { } factory)
                {
                    explicitDefinition =
                        factory(discoveryContext.DescriptorContext, discoveryContext.Scope);
                }

                if (originalType is null ||
                    !_typeRegistry.TryGetType(originalType, out var registeredType))
                {
                    throw Sorting_FieldHadNoType(sortField.Name, parentTypeDefinition.Name);
                }

                if (!_definitions.TryGetValue(
                        registeredType.Type,
                        out var definition))
                {
                    throw Sorting_DefinitionForTypeNotFound(
                        sortField.Name,
                        parentTypeDefinition.Name,
                        registeredType.Type.Name);
                }

                return new SortInputType(
                    definition,
                    explicitDefinition,
                    typeReference,
                    originalType!,
                    sortField);
            }
        });
    }

    private void OnBeforeCompleteName(
        ITypeCompletionContext completionContext,
        SortInputTypeDefinition definition)
    {
        var convention =
            GetConvention(completionContext.DescriptorContext, definition.Scope);

        var descriptor = SortInputTypeDescriptor.New(
            completionContext.DescriptorContext,
            definition.EntityType!,
            definition.Scope);

        var typeReference =
            TypeReference.Create(completionContext.Type, definition.Scope);

        convention.ApplyConfigurations(typeReference, descriptor);

        DataTypeExtensionHelper.MergeSortInputTypeDefinitions(
            completionContext,
            descriptor.CreateDefinition(),
            definition);

        if (!string.IsNullOrEmpty(definition.Name) &&
            definition is IHasScope { Scope: not null })
        {
            definition.Name = completionContext.Scope + "_" + definition.Name;
        }
    }

    private void OnBeforeCompleteName(
        ITypeCompletionContext completionContext,
        SortEnumTypeDefinition definition)
    {
        var convention =
            GetConvention(completionContext.DescriptorContext, definition.Scope);

        var descriptor = SortEnumTypeDescriptor.New(
            completionContext.DescriptorContext,
            definition.EntityType,
            definition.Scope);

        var typeReference =
            TypeReference.Create(completionContext.Type, definition.Scope);

        convention.ApplyConfigurations(typeReference, descriptor);

        DataTypeExtensionHelper.MergeSortEnumTypeDefinitions(
            completionContext,
            descriptor.CreateDefinition(),
            definition);

        if (!string.IsNullOrEmpty(definition.Name) &&
            definition is IHasScope { Scope: not null })
        {
            definition.Name = completionContext.Scope + "_" + definition.Name;
        }
    }

    private void OnBeforeCompleteType(
        ITypeCompletionContext completionContext,
        SortInputTypeDefinition definition)
    {
        var convention = GetConvention(completionContext.DescriptorContext, definition.Scope);

        foreach (var field in definition.Fields)
        {
            if (field is SortFieldDefinition sortFieldDefinition)
            {
                if (sortFieldDefinition.Type is null)
                {
                    throw Sorting_FieldHadNoType(field.Name, definition.Name);
                }

                if (completionContext.TryPredictTypeKind(sortFieldDefinition.Type, out var kind) &&
                    kind != TypeKind.Enum)
                {
                    field.Type = field.Type!.With(scope: completionContext.Scope);
                }

                sortFieldDefinition.Metadata =
                    convention.CreateMetaData(completionContext, definition, sortFieldDefinition);

                if (sortFieldDefinition.Handler is null)
                {
                    if (convention.TryGetFieldHandler(
                        completionContext,
                        definition,
                        sortFieldDefinition,
                        out var handler))
                    {
                        sortFieldDefinition.Handler = handler;
                    }
                    else
                    {
                        throw SortInterceptor_NoFieldHandlerFoundForField(
                            definition,
                            sortFieldDefinition);
                    }
                }
            }
        }
    }

    private void OnBeforeCompleteType(
        ITypeCompletionContext completionContext,
        SortEnumTypeDefinition definition)
    {
        var convention =
            GetConvention(completionContext.DescriptorContext, completionContext.Scope);

        foreach (var enumValue in definition.Values)
        {
            if (enumValue is SortEnumValueDefinition sortEnumValueDefinition)
            {
                if (convention.TryGetOperationHandler(
                    completionContext,
                    definition,
                    sortEnumValueDefinition,
                    out var handler))
                {
                    sortEnumValueDefinition.Handler = handler;
                }
                else
                {
                    throw SortInterceptor_NoOperationHandlerFoundForValue(
                        definition,
                        sortEnumValueDefinition);
                }
            }
        }
    }

    private ISortConvention GetConvention(IDescriptorContext context, string? scope)
    {
        if (!_conventions.TryGetValue(scope ?? string.Empty, out var convention))
        {
            convention = context.GetSortConvention(scope);
            _conventions[scope ?? string.Empty] = convention;
        }

        return convention;
    }

    public override IEnumerable<ITypeReference> RegisterMoreTypes(
        IReadOnlyCollection<ITypeDiscoveryContext> discoveryContexts)
    {
        if (_typesToRegister.Count == 0)
        {
            return Array.Empty<ITypeReference>();
        }

        var typesToRegister = _typesToRegister
            .Select(x => x())
            .ToArray();

        _typesToRegister.Clear();
        return typesToRegister;
    }
}
