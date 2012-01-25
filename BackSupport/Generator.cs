using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace BackSupport
{
    public interface IFileUtils
    {
        void WriteFile(string filename, string contents);
    }

    public class FileUtils : IFileUtils
    {
        public void WriteFile(string filename, string contents)
        {
            File.WriteAllText(filename, contents);
        }
    }

    public class Generator
    {
        private readonly GeneratorOptions _options;
        private readonly Dictionary<Assembly, Regex> _filter = new Dictionary<Assembly, Regex>();
        private readonly IFileUtils _fileUtils;

        public Generator(GeneratorOptions options)
        {
            _options = options;
            _fileUtils = new FileUtils();
        }

        public Generator(GeneratorOptions options, IFileUtils fileUtils)
        {
            _fileUtils = fileUtils;
            _options = options;
        }

        public Generator AddFilter(Assembly assembly, Regex matcher)
        {
            _filter.Add(assembly, matcher);
            return this;
        }


        public void Generate()
        {
            _options.Validate();
            var matchingTypes = new List<Type>();
            foreach(var assembly in _filter.Keys)
            {
                var f = _filter[assembly];
                foreach (var t in assembly.GetTypes())
                    if (f.IsMatch(t.FullName))
                        matchingTypes.Add(t);
            }
            var sb = new StringBuilder();
            sb.AppendLine(@"
(function() {
  __extends = function(child, parent) {
    for (var key in parent) { if (Object.prototype.hasOwnProperty.call(parent, key)) child[key] = parent[key]; }
    function ctor() { this.constructor = child; }
    ctor.prototype = parent.prototype;
    child.prototype = new ctor;
    child.__super__ = parent.prototype;
    return child;
  };
            ");
            foreach (var t in matchingTypes)
            {
                sb.Append(GenerateNamespace(t));
                sb.Append(GenerateType(t));
            }
            sb.AppendLine(@"
})();
            ");
            _fileUtils.WriteFile(_options.OutputFile, sb.ToString());
        }

        private readonly List<string> _namespaceCache = new List<string>(); 
        private string GenerateNamespace(Type type)
        {
            var namespaceComponents = type.Namespace.Split('.');
            string currentNamespace = "";
            var sb = new StringBuilder();
            foreach (var namespaceComponent in namespaceComponents)
            {
                if (currentNamespace != "")
                    currentNamespace += ".";
                currentNamespace += namespaceComponent;
                if (!_namespaceCache.Contains(currentNamespace))
                {
                    sb.Append("this.").Append(currentNamespace).Append(" = ").Append("this.").Append(currentNamespace).Append(" = ").Append("this.").Append(currentNamespace).AppendLine(" || {};");
                    _namespaceCache.Add(currentNamespace);
                }
            }
            return sb.ToString();
        }

        internal string GenerateType(Type type)
        {
            EntityDefinition entityDefinition = GetEntityDefinition(type);
            var sb = new StringBuilder();
            // generate class definition
            var jsShortName = entityDefinition.JsName.Substring(entityDefinition.JsName.LastIndexOf('.') + 1);
            sb.Append("this.").Append(entityDefinition.JsName).AppendLine(" = (function() {");
            if (!string.IsNullOrEmpty(entityDefinition.JsBaseClassName))
                sb.Append("  __extends(").Append(jsShortName).Append(", ").Append(entityDefinition.JsBaseClassName).AppendLine(");");
            // generate constructor
            sb.Append("  function ").Append(jsShortName).AppendLine("() {");
            if (!string.IsNullOrEmpty(entityDefinition.JsBaseClassName))
                sb.Append("    ").Append(jsShortName).AppendLine(".__super__.constructor.apply(this, arguments);");
            sb.AppendLine("  }");
            // generate field definition
            sb.Append("  ").Append(jsShortName).AppendLine(".prototype.fields = {");
            for (var i = 0; i < entityDefinition.Fields.Length; i++)
            {
                var fd = entityDefinition.Fields[i];
                var tn = fd.Type.Type.FullName;
                if (tn.StartsWith("System.Nullable`1[["))
                    tn = fd.Type.Type.GetGenericArguments()[0].FullName + "?";
                if (tn.StartsWith("System."))
                    tn = tn.Substring(7);
                sb.Append("    '").Append(fd.Name).Append("': {'type': '").Append(tn).Append("', 'validations': [");
                for (var j = 0; j < fd.Validations.Length; j++)
                {
                    var vd = fd.Validations[j];
                    sb.Append(vd.GetJsValidationSnippet());
                    if (j < fd.Validations.Length - 1)
                        sb.Append(", ");
                }
                sb.Append("]}");
                if (i == entityDefinition.Fields.Length - 1)
                    sb.AppendLine();
                else
                    sb.AppendLine(",");
            }
            sb.AppendLine("  };");
            // generate class definition close
            sb.Append("  return ").Append(jsShortName).AppendLine(";").AppendLine("}).call(this);");
            return sb.ToString();
        }

        internal EntityDefinition GetEntityDefinition(Type type)
        {
            var ed = new EntityDefinition();
            ed.DotNetName = type.FullName;
            ed.JsName = _options.EntityJsNameTransformer(type.FullName);
            if (!string.IsNullOrEmpty(_options.EntityJsBaseClass))
                ed.JsBaseClassName = _options.EntityJsBaseClass;
            var properties = type.GetProperties();
            ed.Fields = new FieldDefinition[properties.Length];
            for (var i = 0; i < properties.Length; i++ )
            {
                ed.Fields[i] = GetPropertyDefinition(properties[i]);
            }
            return ed;
        }

        internal FieldDefinition GetPropertyDefinition(PropertyInfo propertyInfo)
        {
            var fd = new FieldDefinition();
            fd.Name = propertyInfo.Name;
            fd.Type = _options.TypeDefinitionProvider.GetTypeDefinition(propertyInfo.PropertyType);
            fd.Validations = GetValidations(propertyInfo);
            return fd;
        }

        internal IValidationDefinition[] GetValidations(PropertyInfo propertyInfo)
        {
            var vals = new List<IValidationDefinition>();
            foreach (Attribute attr in propertyInfo.GetCustomAttributes(true))
            {
                var vd2 = _options.ValidationDefinitionProvider.GetValidationDefinition(attr, propertyInfo);
                if (vd2 != null)
                    vals.Add(vd2);
            }
            return vals.ToArray();
        }
    }

    public interface IValidationDefinitionProvider
    {
        IValidationDefinition GetValidationDefinition(Attribute attr, PropertyInfo property);
    }

    public class DefaultValidationDefinitionProvider : IValidationDefinitionProvider
    {
        private readonly Dictionary<Type, Type> _validationDefinitionsForAttributes = new Dictionary<Type, Type>();

        public DefaultValidationDefinitionProvider()
        {
            SetupDefaultValidations();
        }

        public IValidationDefinition GetValidationDefinition(Attribute attr, PropertyInfo property)
        {
            if (_validationDefinitionsForAttributes.ContainsKey(attr.GetType()))
            {
                var val = Activator.CreateInstance(_validationDefinitionsForAttributes[attr.GetType()]) as IValidationDefinition;
                val.Attribute = attr;
                val.Property = property;
                return val;
            }
            return null;
        }

        public void SetValidationDefinition(Type attr, Type validationDefinition)
        {
            _validationDefinitionsForAttributes[attr] = validationDefinition;
        }

        public void SetupDefaultValidations()
        {
            SetValidationDefinition(typeof(RequiredAttribute), typeof(RequiredValidation));
            SetValidationDefinition(typeof(RegularExpressionAttribute), typeof(RegexValidation));
            SetValidationDefinition(typeof(StringLengthAttribute), typeof(StringLengthValidation));
            SetValidationDefinition(typeof(RangeAttribute), typeof(RangeValidation));
        }
    }

    public interface ITypeDefinitionProvider
    {
        TypeDefinition GetTypeDefinition(Type type);
    }

    public class DefaultTypeDefinitionProvider : ITypeDefinitionProvider
    {
        public DefaultTypeDefinitionProvider()
        {
            SetupDefaultTypes();
        }

        private readonly Dictionary<Type, TypeDefinition> _typeDefinitions = new Dictionary<Type, TypeDefinition>();
        public TypeDefinition GetTypeDefinition(Type type)
        {
            var key = _typeDefinitions.Keys.LastOrDefault(x => x == type || x.IsAssignableFrom(type));
            if (key == null)
                throw new ArgumentException("could not find a matching TypeDefinition for type '" + type.FullName + "' - unable to convert this .Net type to a JavaScript type.");
            return _typeDefinitions[key];
        }
        public void SetTypeDefinition(Type type, TypeDefinition definition)
        {
            _typeDefinitions[type] = definition;
        }
        private void SetupDefaultTypes()
        {
            // must be first value
            SetTypeDefinition(typeof(object), new TypeDefinition { JsDocTypeName = "object", JsParseFnSnippet = "BackSupport.Parse.Object", JsTypeName = "object", JsValidateFnSnippet = "BackSupport.Validate.Type.Object", Type = typeof(object) });

            SetTypeDefinition(typeof(string), new TypeDefinition { JsDocTypeName = "string", JsParseFnSnippet = "BackSupport.Parse.String", JsTypeName = "string", JsValidateFnSnippet = "BackSupport.Validate.Type.String", Type = typeof(string) });
            SetTypeDefinition(typeof(char), new TypeDefinition { JsDocTypeName = "char", JsParseFnSnippet = "BackSupport.Parse.Char", JsTypeName = "char", JsValidateFnSnippet = "BackSupport.Validate.Type.Char", Type = typeof(char) });
            SetTypeDefinition(typeof(int), new TypeDefinition { JsDocTypeName = "number", JsParseFnSnippet = "BackSupport.Parse.Integer", JsTypeName = "number", JsValidateFnSnippet = "BackSupport.Validate.Type.Integer", Type = typeof(int) });
            SetTypeDefinition(typeof(long), new TypeDefinition { JsDocTypeName = "number", JsParseFnSnippet = "BackSupport.Parse.Integer", JsTypeName = "number", JsValidateFnSnippet = "BackSupport.Validate.Type.Integer", Type = typeof(long) });
            SetTypeDefinition(typeof(double), new TypeDefinition { JsDocTypeName = "number", JsParseFnSnippet = "BackSupport.Parse.Float", JsTypeName = "number", JsValidateFnSnippet = "BackSupport.Validate.Type.Float", Type = typeof(double) });
            SetTypeDefinition(typeof(float), new TypeDefinition { JsDocTypeName = "number", JsParseFnSnippet = "BackSupport.Parse.Float", JsTypeName = "number", JsValidateFnSnippet = "BackSupport.Validate.Type.Float", Type = typeof(float) });
            SetTypeDefinition(typeof(bool), new TypeDefinition { JsDocTypeName = "boolean", JsParseFnSnippet = "BackSupport.Parse.Boolean", JsTypeName = "boolean", JsValidateFnSnippet = "BackSupport.Validate.Type.Boolean", Type = typeof(bool) });
            SetTypeDefinition(typeof(DateTime), new TypeDefinition { JsDocTypeName = "date", JsParseFnSnippet = "BackSupport.Parse.DateTime", JsTypeName = "date", JsValidateFnSnippet = "BackSupport.Validate.Type.DateTime", Type = typeof(DateTime) });
            SetTypeDefinition(typeof(Nullable<char>), new TypeDefinition { JsDocTypeName = "char", JsParseFnSnippet = "BackSupport.Parse.Char", JsTypeName = "char", JsValidateFnSnippet = "BackSupport.Validate.Type.NullableChar", Type = typeof(char) });
            SetTypeDefinition(typeof(Nullable<int>), new TypeDefinition { JsDocTypeName = "number", JsParseFnSnippet = "BackSupport.Parse.Integer", JsTypeName = "number", JsValidateFnSnippet = "BackSupport.Validate.Type.NullableInteger", Type = typeof(Nullable<int>) });
            SetTypeDefinition(typeof(Nullable<long>), new TypeDefinition { JsDocTypeName = "number", JsParseFnSnippet = "BackSupport.Parse.Integer", JsTypeName = "number", JsValidateFnSnippet = "BackSupport.Validate.Type.NullableInteger", Type = typeof(Nullable<long>) });
            SetTypeDefinition(typeof(Nullable<double>), new TypeDefinition { JsDocTypeName = "number", JsParseFnSnippet = "BackSupport.Parse.Float", JsTypeName = "number", JsValidateFnSnippet = "BackSupport.Validate.Type.NullableFloat", Type = typeof(Nullable<double>) });
            SetTypeDefinition(typeof(Nullable<float>), new TypeDefinition { JsDocTypeName = "number", JsParseFnSnippet = "BackSupport.Parse.Float", JsTypeName = "number", JsValidateFnSnippet = "BackSupport.Validate.Type.NullableFloat", Type = typeof(Nullable<float>) });
            SetTypeDefinition(typeof(Nullable<bool>), new TypeDefinition { JsDocTypeName = "boolean", JsParseFnSnippet = "BackSupport.Parse.Boolean", JsTypeName = "boolean", JsValidateFnSnippet = "BackSupport.Validate.Type.NullableBoolean", Type = typeof(Nullable<bool>) });
            SetTypeDefinition(typeof(Nullable<DateTime>), new TypeDefinition { JsDocTypeName = "date", JsParseFnSnippet = "BackSupport.Parse.DateTime", JsTypeName = "date", JsValidateFnSnippet = "BackSupport.Validate.Type.NullableDateTime", Type = typeof(Nullable<DateTime>) });
        }
    }

    public class GeneratorOptions
    {
        public GeneratorOptions()
        {
            EntityJsNameTransformer = x => x;
            EntityJsBaseClass = "Backbone.Model";
            ValidationDefinitionProvider = new DefaultValidationDefinitionProvider();
            TypeDefinitionProvider = new DefaultTypeDefinitionProvider();
        }

        public ITypeDefinitionProvider TypeDefinitionProvider { get; set; }
        public IValidationDefinitionProvider ValidationDefinitionProvider { get; set; }
        public Func<string, string> EntityJsNameTransformer { get; set; }
        public string EntityJsBaseClass { get; set; }
        public string OutputFile { get; set; }
        public void Validate()
        {
            if (OutputFile == null)
                throw new ArgumentException("The OutputFile must be set");
            if (!new FileInfo(OutputFile).Directory.Exists)
                throw new ArgumentException("The OutputFile parent directory must exist");
            if (TypeDefinitionProvider == null)
                throw new ArgumentException("The TypeDefinitionProvider must be set");
            if (ValidationDefinitionProvider == null)
                throw new ArgumentException("The ValidationDefinitionProvider must be set");
            if (EntityJsNameTransformer == null)
                throw new ArgumentException("The EntityJsNameTransformer must be set");
        }
    }

    public class EntityDefinition
    {
        public string DotNetName { get; set; }
        public string JsName { get; set; }
        public string JsBaseClassName { get; set; }
        public FieldDefinition[] Fields { get; set; }
    }

    public class FieldDefinition
    {
        public string Name { get; set; }
        public TypeDefinition Type { get; set; }
        public IValidationDefinition[] Validations { get; set; }
    }

    public class TypeDefinition
    {
        public Type Type { get; set; }
        public string JsTypeName { get; set; }
        public string JsDocTypeName { get; set; }
        public string JsParseFnSnippet { get; set; }
        public string JsValidateFnSnippet { get; set; }
    }

    public interface IValidationDefinition
    {
        Attribute Attribute { get; set; }
        PropertyInfo Property { get; set; }
        String GetJsValidationSnippet();
        string JsFieldName { get; }
    }

    public abstract class BaseValidationDefinition<T> : IValidationDefinition
        where T : Attribute
    {
        public Attribute Attribute
        {
            get;
            set;
        }

        public PropertyInfo Property
        {
            get; set;
        }

        /// <summary>
        /// Get the value from the DisplayAttribute of the property if defined, else use the field name
        /// </summary>
        public virtual string JsFieldName
        {
            get
            {
                var attr = Property.GetCustomAttributes(typeof(DisplayAttribute), true);
                if (attr != null && attr.Length > 0)
                    return (attr[0] as DisplayAttribute).Description;
                return Property.Name;
            }
            set { }
        }

        public abstract string GetJsValidationSnippet();

        public string JsMethodName { get; set; }
    }
                //SetValidationDefinition(typeof(RequiredAttribute), new ValidationDefinition { DotNetName = typeof(RequiredAttribute).FullName, JsMethodName = "BackSupport.Validate.Required" });
                //SetValidationDefinition(typeof(RegularExpressionAttribute), new ValidationDefinition { DotNetName = typeof(RegularExpressionAttribute).FullName, JsMethodName = "BackSupport.Validate.Regex" });
                //SetValidationDefinition(typeof(StringLengthAttribute), new ValidationDefinition { DotNetName = typeof(StringLengthAttribute).FullName, JsMethodName = "BackSupport.Validate.StringLength" });
                //SetValidationDefinition(typeof(RangeAttribute), new ValidationDefinition { DotNetName = typeof(RangeAttribute).FullName, JsMethodName = "BackSupport.Validate.Range" });

    public class RequiredValidation : BaseValidationDefinition<RequiredAttribute>
    {
        public override string GetJsValidationSnippet()
        {
            return "BackSupport.Validate.Required({ fieldName: '" + JsFieldName + "'})";
        }
    }

    public class StringLengthValidation : BaseValidationDefinition<StringLengthAttribute>
    {
        public override string GetJsValidationSnippet()
        {
            var sb = new StringBuilder("BackSupport.Validate.StringLength({ fieldName: '").Append(JsFieldName).Append("'");
            var attribute = Attribute as StringLengthAttribute;
            if (attribute == null)
                throw new ArgumentException("Attempt to initialise a " + this.GetType().FullName + " with an attribute of type " + Attribute.GetType().FullName);
            if (attribute.MaximumLength > 0)
                sb.Append(", 'maxLength': ").Append(attribute.MaximumLength);
            if (attribute.MinimumLength > 0)
                sb.Append(", 'minLength': ").Append(attribute.MinimumLength);
            sb.Append("})");
            return sb.ToString();
        }
    }

    public class RangeValidation : BaseValidationDefinition<RangeAttribute>
    {
        public override string GetJsValidationSnippet()
        {
            var sb = new StringBuilder("BackSupport.Validate.Range({ fieldName: '").Append(JsFieldName).Append("'");
            var attribute = Attribute as RangeAttribute;
            if (attribute == null)
                throw new ArgumentException("Attempt to initialise a " + this.GetType().FullName + " with an attribute of type " + Attribute.GetType().FullName);
            if (attribute.Minimum != null)
                sb.Append(", 'min': ").Append(attribute.Minimum.ToString());
            if (attribute.Maximum != null)
                sb.Append(", 'max': ").Append(attribute.Maximum.ToString());
            sb.Append("})");
            return sb.ToString();
        }
    }

    public class RegexValidation : BaseValidationDefinition<RegularExpressionAttribute>
    {
        public override string GetJsValidationSnippet()
        {
            var sb = new StringBuilder("BackSupport.Validate.Range({ fieldName: '").Append(JsFieldName).Append("'");
            var attribute = Attribute as RegularExpressionAttribute;
            if (attribute == null)
                throw new ArgumentException("Attempt to initialise a " + this.GetType().FullName + " with an attribute of type " + Attribute.GetType().FullName);
            if (attribute.Pattern != null)
                sb.Append(", 'regex': '").Append(attribute.Pattern.Replace("'", "\\'")).Append("'");
            sb.Append("})");
            return sb.ToString();
        }
    }

}
