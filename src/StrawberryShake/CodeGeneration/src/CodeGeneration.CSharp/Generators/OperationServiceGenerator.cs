using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StrawberryShake.CodeGeneration.CSharp.Builders;
using StrawberryShake.CodeGeneration.CSharp.Extensions;
using StrawberryShake.CodeGeneration.Descriptors.Operations;
using StrawberryShake.CodeGeneration.Extensions;
using StrawberryShake.CodeGeneration.Properties;
using static StrawberryShake.CodeGeneration.CSharp.Generators.InputValueFormatterGenerator;
using static StrawberryShake.CodeGeneration.Descriptors.NamingConventions;
using static StrawberryShake.CodeGeneration.Utilities.NameUtils;

namespace StrawberryShake.CodeGeneration.CSharp.Generators
{
    public class OperationServiceGenerator : ClassBaseGenerator<OperationDescriptor>
    {
        private const string _variables = "variables";
        private const string _operationExecutor = "_operationExecutor";
        private const string _createRequest = "CreateRequest";
        private const string _strategy = "strategy";
        private const string _serializerResolver = "serializerResolver";
        private const string _request = "request";
        private const string _value = "value";
        private const string _cancellationToken = "cancellationToken";

        protected override void Generate(
            CodeWriter writer,
            OperationDescriptor descriptor,
            out string fileName)
        {
            fileName = descriptor.RuntimeType.Name;

            ClassBuilder classBuilder = ClassBuilder
                .New()
                .SetComment(
                    XmlCommentBuilder
                        .New()
                        .SetSummary(
                            string.Format(
                                CodeGenerationResources.OperationServiceDescriptor_Description,
                                descriptor.Name))
                        .AddCode(descriptor.BodyString))
                .SetName(fileName);

            ConstructorBuilder constructorBuilder = classBuilder
                .AddConstructor()
                .SetTypeName(fileName);

            var runtimeTypeName =
                descriptor.ResultTypeReference.GetRuntimeType().Name;

            AddConstructorAssignedField(
                TypeNames.IOperationExecutor.WithGeneric(runtimeTypeName),
                _operationExecutor,
                classBuilder,
                constructorBuilder);

            AddInjectedSerializers(descriptor, constructorBuilder, classBuilder);

            if (descriptor is not SubscriptionOperationDescriptor)
            {
                classBuilder.AddMethod(CreateExecuteMethod(descriptor, runtimeTypeName));
            }

            classBuilder.AddMethod(CreateWatchMethod(descriptor, runtimeTypeName));
            classBuilder.AddMethod(CreateRequestMethod(descriptor));

            AddFormatMethods(descriptor, classBuilder);

            CodeFileBuilder
                .New()
                .SetNamespace(descriptor.RuntimeType.NamespaceWithoutGlobal)
                .AddType(classBuilder)
                .Build(writer);
        }

        private static void AddFormatMethods(
            OperationDescriptor descriptor,
            ClassBuilder classBuilder)
        {
            foreach (var argument in descriptor.Arguments)
            {
                classBuilder
                    .AddMethod()
                    .SetPrivate()
                    .SetReturnType(TypeNames.Object.MakeNullable())
                    .SetName("Format" + GetPropertyName(argument.Name))
                    .AddParameter(_value, x => x.SetType(argument.Type.ToBuilder()))
                    .AddCode(GenerateSerializer(argument.Type, _value));
            }
        }

        private static void AddInjectedSerializers(
            OperationDescriptor descriptor,
            ConstructorBuilder constructorBuilder,
            ClassBuilder classBuilder)
        {
            var neededSerializers = descriptor
                .Arguments
                .GroupBy(x => x.Type.Name)
                .ToDictionary(x => x.Key, x => x.First());

            if (!neededSerializers.Any())
            {
                return;
            }

            constructorBuilder
                .AddParameter(_serializerResolver)
                .SetType(TypeNames.ISerializerResolver);

            foreach (var property in neededSerializers.Values)
            {
                if (property.Type.GetName().Value is { } name)
                {
                    var fieldName = $"{GetFieldName(name)}Formatter";
                    constructorBuilder
                        .AddCode(
                            AssignmentBuilder
                                .New()
                                .SetLefthandSide(fieldName)
                                .SetRighthandSide(
                                    MethodCallBuilder
                                        .Inline()
                                        .SetMethodName(
                                            _serializerResolver,
                                            "GetInputValueFormatter")
                                        .AddArgument(name.AsStringToken())));

                    classBuilder
                        .AddField()
                        .SetName(fieldName)
                        .SetAccessModifier(AccessModifier.Private)
                        .SetType(TypeNames.IInputValueFormatter)
                        .SetReadOnly();
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Serializer for property {descriptor.RuntimeType.Name}." +
                        $"{property.Name} could not be created. GraphQLTypeName was empty");
                }
            }
        }

        private MethodCallBuilder CreateRequestMethodCall(OperationDescriptor operationDescriptor)
        {
            MethodCallBuilder createRequestMethodCall = MethodCallBuilder
                .Inline()
                .SetMethodName(_createRequest);

            foreach (var arg in operationDescriptor.Arguments)
            {
                createRequestMethodCall.AddArgument(GetParameterName(arg.Name));
            }

            return createRequestMethodCall;
        }

        private MethodBuilder CreateWatchMethod(
            OperationDescriptor descriptor,
            string runtimeTypeName)
        {
            MethodBuilder watchMethod =
                MethodBuilder
                    .New()
                    .SetPublic()
                    .SetReturnType(
                        TypeNames.IOperationObservable
                            .WithGeneric(TypeNames.IOperationResult.WithGeneric(runtimeTypeName)))
                    .SetName(TypeNames.Watch);

            foreach (var arg in descriptor.Arguments)
            {
                watchMethod
                    .AddParameter()
                    .SetName(GetParameterName(arg.Name))
                    .SetType(arg.Type.ToBuilder());
            }

            watchMethod.AddParameter()
                .SetName(_strategy)
                .SetType(TypeNames.ExecutionStrategy.MakeNullable())
                .SetDefault("null");

            return watchMethod
                .AddCode(
                    AssignmentBuilder
                        .New()
                        .SetLefthandSide($"var {_request}")
                        .SetRighthandSide(CreateRequestMethodCall(descriptor)))
                .AddCode(
                    MethodCallBuilder
                        .New()
                        .SetReturn()
                        .SetMethodName(_operationExecutor, "Watch")
                        .AddArgument(_request)
                        .AddArgument(_strategy));
        }

        private MethodBuilder CreateExecuteMethod(
            OperationDescriptor operationDescriptor,
            string runtimeTypeName)
        {
            MethodBuilder executeMethod = MethodBuilder
                .New()
                .SetPublic()
                .SetAsync()
                .SetReturnType(
                    TypeNames.Task.WithGeneric(
                        TypeNames.IOperationResult.WithGeneric(runtimeTypeName)))
                .SetName(TypeNames.Execute);

            foreach (var arg in operationDescriptor.Arguments)
            {
                executeMethod
                    .AddParameter()
                    .SetName(GetParameterName(arg.Name))
                    .SetType(arg.Type.ToBuilder());
            }

            executeMethod
                .AddParameter(_cancellationToken)
                .SetType(TypeNames.CancellationToken)
                .SetDefault();

            return executeMethod
                .AddCode(
                    AssignmentBuilder
                        .New()
                        .SetLefthandSide($"var {_request}")
                        .SetRighthandSide(CreateRequestMethodCall(operationDescriptor)))
                .AddEmptyLine()
                .AddCode(
                    MethodCallBuilder
                        .New()
                        .SetReturn()
                        .SetAwait()
                        .SetMethodName(_operationExecutor, "ExecuteAsync")
                        .AddArgument(_request)
                        .AddArgument(_cancellationToken)
                        .Chain(x => x
                            .SetMethodName(nameof(Task.ConfigureAwait))
                            .AddArgument("false")));
        }

        private MethodBuilder CreateRequestMethod(OperationDescriptor descriptor)
        {
            string typeName = CreateDocumentTypeName(descriptor.RuntimeType.Name);

            MethodBuilder method = MethodBuilder
                .New()
                .SetName(_createRequest)
                .SetReturnType(TypeNames.OperationRequest);

            MethodCallBuilder newOperationRequest = MethodCallBuilder
                .New()
                .SetReturn()
                .SetNew()
                .SetMethodName(TypeNames.OperationRequest)
                .AddArgument($"id: {typeName}.Instance.Hash.Value")
                .AddArgument("name: " + descriptor.Name.AsStringToken())
                .AddArgument($"document: {typeName}.Instance")
                .AddArgument($"strategy: {TypeNames.RequestStrategy}.{descriptor.Strategy}");

            if (descriptor.Arguments.Count > 0)
            {
                method
                    .AddCode(
                        AssignmentBuilder
                            .New()
                            .SetLefthandSide($"var {_variables}")
                            .SetRighthandSide(
                                MethodCallBuilder
                                    .Inline()
                                    .SetNew()
                                    .SetMethodName(TypeNames.Dictionary)
                                    .AddGeneric(TypeNames.String)
                                    .AddGeneric(TypeNames.Object.MakeNullable())))
                    .AddEmptyLine();

                foreach (var arg in descriptor.Arguments)
                {
                    var argName = GetParameterName(arg.Name);

                    method.AddParameter(argName, x => x.SetType(arg.Type.ToBuilder()));

                    method.AddCode(
                        MethodCallBuilder
                            .New()
                            .SetMethodName(_variables, nameof(Dictionary<object, object>.Add))
                            .AddArgument(arg.Name.AsStringToken())
                            .AddArgument(
                                MethodCallBuilder
                                    .Inline()
                                    .SetMethodName($"Format{GetPropertyName(arg.Name)}")
                                    .AddArgument(argName)));
                }

                newOperationRequest.AddArgument("variables:" + _variables);
            }

            return method
                .AddEmptyLine()
                .AddCode(newOperationRequest);
        }
    }
}
