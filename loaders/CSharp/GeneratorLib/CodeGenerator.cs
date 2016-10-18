﻿using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CSharp;

namespace GeneratorLib
{
    public class CodeGenerator
    {
        private string m_directory;
        private string m_rootSchemaName;

        public CodeGenerator(string rootSchemaFilePath)
        {
            rootSchemaFilePath = Path.GetFullPath(rootSchemaFilePath);
            m_directory = Path.GetDirectoryName(rootSchemaFilePath);
            m_rootSchemaName = Path.GetFileName(rootSchemaFilePath);
        }

        public Dictionary<string, Schema> FileSchemas { get; private set; }

        public void ParseSchemas()
        {
            FileSchemas = new SchemaParser(m_directory).ParseSchemaTree(m_rootSchemaName);
        }

        public void ExpandSchemaReferences()
        {
            ExpandSchemaReferences(FileSchemas[m_rootSchemaName]);
        }

        private void ExpandSchemaReferences(Schema schema)
        {
            foreach (var typeReference in new TypeReferenceEnumerator(schema))
            {
                if (typeReference.IsReference)
                {
                    ExpandSchemaReferences(FileSchemas[typeReference.Name]);
                }
            }

            if (schema.Properties != null)
            {
                var keys = schema.Properties.Keys.ToArray();
                foreach (var key in keys)
                {
                    if (!string.IsNullOrEmpty(schema.Properties[key].ReferenceType))
                    {
                        schema.Properties[key] = FileSchemas[schema.Properties[key].ReferenceType];
                    }

                    ExpandSchemaReferences(schema.Properties[key]);
                }
            }

            if (schema.DictionaryValueType != null)
            {
                if (!string.IsNullOrEmpty(schema.DictionaryValueType.ReferenceType))
                {
                    schema.DictionaryValueType = FileSchemas[schema.DictionaryValueType.ReferenceType];
                }

                ExpandSchemaReferences(schema.DictionaryValueType);
            }

            if (schema.Items != null)
            {
                if (!string.IsNullOrEmpty(schema.Items.ReferenceType))
                {
                    schema.Items = FileSchemas[schema.Items.ReferenceType];
                }

                ExpandSchemaReferences(schema.Items);
            }
        }

        public void EvaluateInheritance()
        {
            EvaluateInheritance(FileSchemas[m_rootSchemaName]);
        }

        private void EvaluateInheritance(Schema schema)
        {
            foreach (var subSchema in new SchemaEnumerator(schema))
            {
                EvaluateInheritance(subSchema);
            }

            foreach (var typeReference in new TypeReferenceEnumerator(schema))
            {
                if (typeReference.IsReference)
                {
                    EvaluateInheritance(FileSchemas[typeReference.Name]);
                }
            }

            if (schema.Extends == null) return;

            // var baseSchema = FileSchemas[schema.Extends.Name];
            // if (baseSchema.Type.Length == 1 && baseSchema.Type[0].Name == "object") return;

            var baseType = FileSchemas[schema.Extends.Name];

            if (schema.Properties != null && baseType.Properties != null)
            {
                foreach (var property in baseType.Properties)
                {
                    schema.Properties.Add(property.Key, property.Value);
                }
            }

            foreach (var property in baseType.GetType().GetProperties())
            {
                if (!property.CanRead || !property.CanWrite) continue;

                if (property.GetValue(schema) == null)
                {
                    property.SetValue(schema, property.GetValue(baseType));
                }
            }

            schema.Extends = null;
        }
        public Dictionary<string, CodeTypeDeclaration> GeneratedClasses { get; set; }

        public CodeCompileUnit RawClass(string fileName, out string className)
        {
            var root = FileSchemas[fileName];
            var schemaFile = new CodeCompileUnit();
            var schemaNamespace = new CodeNamespace("glTFLoader.Schema");
            schemaNamespace.Imports.Add(new CodeNamespaceImport("System.Linq"));

            className = Helpers.ParseTitle(root.Title);

            var schemaClass = new CodeTypeDeclaration(className)
            {
                Attributes = MemberAttributes.Public
            };

            if (root.Extends != null && root.Extends.IsReference)
            {
                schemaClass.BaseTypes.Add(Helpers.ParseTitle(FileSchemas[root.Extends.Name].Title));
            }

            foreach (var property in root.Properties)
            {
                AddProperty(schemaClass, property.Key, property.Value);
            }

            GeneratedClasses[fileName] = schemaClass;
            schemaNamespace.Types.Add(schemaClass);
            //new CodeAttributeDeclaration(new CodeTypeReference(new CodeTypeParameter()))
            schemaFile.Namespaces.Add(schemaNamespace);
            return schemaFile;
        }

        private void AddProperty(CodeTypeDeclaration target, string rawName, Schema schema)
        {
            var name = Helpers.ParsePropertyName(rawName);
            var fieldName = "m_" + name.Substring(0, 1).ToLower() + name.Substring(1);
            var codegenType = CodegenTypeFactory.MakeCodegenType(rawName, schema);
            target.Members.AddRange(codegenType.AdditionalMembers);
            
            var propertyBackingVariable = new CodeMemberField
            {
                Type = codegenType.CodeType,
                Name = fieldName,
                Comments = { new CodeCommentStatement("<summary>", true), new CodeCommentStatement($"Backing field for {name}.", true), new CodeCommentStatement("</summary>", true) },
                InitExpression = codegenType.DefaultValue
            };

            target.Members.Add(propertyBackingVariable);

            var setStatements = codegenType.SetStatements ?? new CodeStatementCollection();
            setStatements.Add(new CodeAssignStatement()
            {
                Left = new CodeFieldReferenceExpression
                {
                    FieldName = fieldName,
                    TargetObject = new CodeThisReferenceExpression()
                },
                Right = new CodePropertySetValueReferenceExpression()
            });

            var property = new CodeMemberProperty
            {
                Type = codegenType.CodeType,
                Name = name,
                Attributes = MemberAttributes.Public | MemberAttributes.Final,
                HasGet = true,
                GetStatements = { new CodeMethodReturnStatement(new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), fieldName)) },
                HasSet = true,
                Comments = { new CodeCommentStatement("<summary>", true), new CodeCommentStatement(schema.Description, true), new CodeCommentStatement("</summary>", true) },
                CustomAttributes = codegenType.Attributes
            };
            property.SetStatements.AddRange(setStatements);

            target.Members.Add(property);
        }

        public static CodeTypeReference GetCodegenType(CodeTypeDeclaration target, Schema schema, string name, out CodeAttributeDeclarationCollection attributes, out CodeExpression defaultValue)
        {
            var codegenType = CodegenTypeFactory.MakeCodegenType(name, schema);
            attributes = codegenType.Attributes;
            defaultValue = codegenType.DefaultValue;
            target.Members.AddRange(codegenType.AdditionalMembers);

            return codegenType.CodeType;
        }

        public void CSharpCodeGen(string outputDirectory)
        {
            GeneratedClasses = new Dictionary<string, CodeTypeDeclaration>();
            foreach (var schema in FileSchemas)
            {
                if (schema.Value.Type != null && schema.Value.Type[0].Name == "object")
                {
                    CodeGenClass(schema.Key, outputDirectory);
                }
            }
        }

        private void CodeGenClass(string fileName, string outputDirectory)
        {
            string className;
            var schemaFile = RawClass(fileName, out className);
            CSharpCodeProvider csharpcodeprovider = new CSharpCodeProvider();
            var sourceFile = Path.Combine(outputDirectory, className + "." + csharpcodeprovider.FileExtension);

            IndentedTextWriter tw1 = new IndentedTextWriter(new StreamWriter(sourceFile, false), "    ");
            csharpcodeprovider.GenerateCodeFromCompileUnit(schemaFile, tw1, new CodeGeneratorOptions());
            tw1.Close();
        }
    }
}
