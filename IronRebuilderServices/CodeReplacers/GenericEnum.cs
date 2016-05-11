using System;
using System.Collections.Generic;
using System.Linq;
using IronRebuilder.Attributes;
using Mono.Cecil;
using Mono.Collections.Generic;

namespace IronRebuilder.CodeReplacers
{
    /// <summary>
    /// This class replaces <see cref="GenericEnumAttribute"/>
    /// </summary>
    /// <seealso cref="IronRebuilder.CodeReplacers.ICodeReplacer" />
    public class GenericEnum : ICodeReplacer
    {
        private static readonly Type EnumType = typeof(Enum);

        private static readonly HashSet<string> AllowedTypes = new HashSet<string>()
        {
            EnumType.FullName,
            typeof(object).FullName,
            typeof(ValueType).FullName
        };

        private readonly Action<string> errorAction;
        private readonly HashSet<MethodDefinition> replacedMethods = new HashSet<MethodDefinition>();
        private readonly HashSet<TypeDefinition> replacedTypes = new HashSet<TypeDefinition>();

        /// <summary>
        /// Initializes a new instance of the <see cref="GenericEnum"/> class.
        /// </summary>
        /// <param name="errorAction">The action to take on an error.</param>
        public GenericEnum(Action<string> errorAction)
        {
            this.errorAction = errorAction;
        }

        /// <summary>
        /// Replaces an attribute, returning true for success and false otherwise
        /// </summary>
        /// <param name="assembly">The assembly</param>
        /// <returns>
        /// Returns if the replacement was successful
        /// </returns>
        public bool Replace(AssemblyDefinition assembly)
        {
            // Get all errors at once so they don't need to recompile for each mistake.
            var success = true;
            var assemRefs = assembly.MainModule.AssemblyReferences.ToArray() ?? new AssemblyNameReference[0];
            IMetadataScope core = assemRefs.FirstOrDefault(r => r.Name == "mscorlib");

            if (core == null)
            {
                core = assemRefs.FirstOrDefault(r => r.Name == "System.Runtime");
            }

            foreach (var module in assembly.Modules)
            {
                var enumType = core == null ? null : new TypeReference("System", "Enum", module, core, true);
                success &= Replace(module.Types, enumType);
            }

            return success;
        }

        /// <summary>
        /// Validate an assembly after all replacements have been done.
        /// </summary>
        /// <param name="assembly">The assembly</param>
        /// <returns> Returns if the replacement was successful </returns>
        public bool Validate(AssemblyDefinition assembly)
        {
            var success = true;
            foreach (var module in assembly.Modules) success &= Validate(module.Types);
            return success;
        }

        private static string GetProperName(TypeDefinition type, Collection<TypeReference> genArgs)
        {
            // TODO use more friendly name that uses <T(,U)*> instead of `#args
            return type.ToString();
        }

        private bool Replace(ICollection<TypeDefinition> types, TypeReference enumType)
        {
            // Get all errors at once so they don't need to recompile for each mistake.
            var success = true;
            foreach (var type in types)
            {
                if (type.HasNestedTypes) success &= Replace(type.NestedTypes, enumType);

                foreach (var method in type.Methods)
                {
                    success &= Replace(method.GenericParameters, enumType, () => replacedMethods.Add(method));
                }

                success &= Replace(type.GenericParameters, enumType, () => replacedTypes.Add(type));
            }

            return success;
        }

        private bool Replace(Collection<GenericParameter> genericParams, TypeReference enumType, Action replacementDone)
        {
            var success = true;
            var replaced = false;

            foreach (var genericParam in genericParams)
            {
                var attribs = genericParam.CustomAttributes;
                var attrib = attribs.FirstOrDefault(a => a.AttributeType.FullName == typeof(GenericEnumAttribute).FullName);
                if (attrib == null)
                {
                    continue;
                }

                if (enumType == null)
                {
                    errorAction($"Currently mscorlib or System.Runtime is required to use {nameof(GenericEnumAttribute)}");
                    return false;
                }

                attribs.Remove(attrib);
                var badConstraints = genericParam.Constraints.Where(c => !AllowedTypes.Contains(c.FullName)).Select(c => c.FullName).ToArray();
                if (badConstraints.Length != 0)
                {
                    errorAction($"Can't make generic parameter {genericParam.Name} extend enum because it extends {string.Join(", ", badConstraints)}");
                    success = false;
                }

                replaced = true;
                genericParam.Constraints.Clear();
                genericParam.Constraints.Add(enumType);
            }

            if (replaced) replacementDone();
            return success;
        }

        private bool Validate(ICollection<TypeDefinition> types)
        {
            var success = true;
            foreach (var type in types)
            {
                if (type.BaseType == null) continue;

                if (type.HasNestedTypes) success &= Validate(type.NestedTypes);

                success &= Validate(type.Methods);
                foreach (var field in type.Fields.Select(f => f.FieldType)) success &= Validate(field);

                foreach (var genericParam in type.GenericParameters) success &= Validate(genericParam);
                success &= Validate(type.BaseType);
                foreach (var @interface in type.Interfaces) success &= Validate(@interface);
            }

            return success;
        }

        private bool Validate(IEnumerable<MethodDefinition> methods)
        {
            var success = true;
            foreach (var method in methods)
            {
                success &= Validate(method.ReturnType);
                foreach (var param in method.Parameters) success &= Validate(param.ParameterType);

                if (!method.HasBody) continue;

                foreach (var type in method.Body.Variables.Select(v => v.VariableType)) success &= Validate(type);
                var operands = method.Body.Instructions.Select(i => i.Operand).ToArray();
                success &= Validate(operands.OfType<MethodReference>());
                foreach (var type in operands.OfType<GenericInstanceType>()) success &= Validate(type);
            }

            return success;
        }

        private bool Validate(IEnumerable<MethodReference> methodRefs)
        {
            var success = true;
            foreach (var methodRef in methodRefs)
            {
                MethodDefinition methodDef;
                try
                {
                    methodDef = methodRef.Resolve();
                }
                catch (AssemblyResolutionException)
                {
                    // Since all the types we recompiled are imported, this is in another assembly.
                    // As we did not recompile it, it could not have changed types since compilation
                    // (at least from what we did).
                    // Therefore, there is safe to skip validation here.
                    return false;
                }

                if (methodRef.IsGenericInstance)
                {
                    var genericMethod = (GenericInstanceMethod)methodRef;
                    success &= Validate(methodDef.GenericParameters, genericMethod.GenericArguments, methodRef.ToString());
                }
                else if (methodDef.IsDefinition && methodRef.DeclaringType.IsGenericInstance)
                {
                    var genericDef = ((MethodDefinition)methodDef).DeclaringType;
                    var genCall = (GenericInstanceType)methodRef.DeclaringType;
                    success &= Validate(genericDef.GenericParameters, genCall.GenericArguments, methodRef.ToString());
                }
            }

            return success;
        }

        private bool Validate(TypeReference refType)
        {
            var specType = refType as TypeSpecification;

            if (specType != null && !Validate(specType.ElementType)) return false;

            if (!refType.IsGenericInstance) return true;

            TypeDefinition resolved;
            try
            {
                resolved = refType.Resolve();
            }
            catch (AssemblyResolutionException)
            {
                // Since all the types we recompiled are imported, this is in another assembly.
                // As we did not recompile it, it could not have changed types since compilation
                // (at least from what we did).
                // Therefore, there is safe to skip validation here.
                return false;
            }

            var genArgs = ((GenericInstanceType)refType).GenericArguments;
            return Validate(resolved.GenericParameters, genArgs, GetProperName(resolved, genArgs));
        }

        private bool Validate<T>(IList<GenericParameter> baseGenericParams, IList<T> genArgs, string name)
            where T : TypeReference
        {
            bool success = true;

            for (int i = 0; i < baseGenericParams.Count; i++)
            {
                var baseGenericParam = baseGenericParams[i];
                var genArgType = genArgs[i];
                if (baseGenericParam.HasConstraints && baseGenericParam.Constraints[0].FullName == EnumType.FullName)
                {
                    if (genArgType.IsGenericParameter)
                    {
                        var gp = genArgType as GenericParameter;
                        if (!gp.HasConstraints || gp.Constraints.Count(t => t.FullName == EnumType.FullName) == 0)
                        {
                            errorAction($"The type '{genArgType.Name}' cannot be used as type parameter '{baseGenericParam.Name}' in the generic type or method '{name}'. There is no boxing conversion or type parameter conversion from '{genArgType.Name}' to 'System.Enum'.");
                            success = false;
                        }
                    }
                    else
                    {
                        if (genArgType.FullName != EnumType.FullName)
                        {
                            var resolve = genArgType.Resolve();
                            if (resolve != null && resolve.BaseType.FullName == EnumType.FullName)
                            {
                                continue;
                            }

                            var msg = resolve == null ?
                                $"There is no implicit reference conversion from '{genArgType.FullName}' to 'System.Enum'." :
                                string.Format(
                                    "The type '{0}' cannot be used as type parameter '{1}' in the generic type or method '{2}'. There is no boxing conversion or type parameter conversion from '{0}' to 'System.Enum'.",
                                    genArgType.FullName,
                                    baseGenericParam.Name,
                                    name);

                            errorAction(msg);
                            success = false;
                        }
                    }
                }

                success &= Validate(genArgType);
            }

            return success;
        }
    }
}