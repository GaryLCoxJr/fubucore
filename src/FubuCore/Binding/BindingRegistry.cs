using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FubuCore.Conversion;
using FubuCore.Reflection;
using FubuCore.Util;

namespace FubuCore.Binding
{
    public class BindingRegistry : IModelBinderCache, IValueConverterRegistry, IPropertyBinderCache
    {
        private readonly IList<IModelBinder> _binders = new List<IModelBinder>();
        private readonly Cache<Type, IModelBinder> _modelBinderCache = new Cache<Type, IModelBinder>();
        private readonly Cache<PropertyInfo, IPropertyBinder> _propertyBinderCache = new Cache<PropertyInfo, IPropertyBinder>();
        private Lazy<StandardModelBinder> _standardBinder;
        private Lazy<IEnumerable<IPropertyBinder>> _defaultPropertyBinders;
        private readonly IList<IPropertyBinder> _propertyBinders = new List<IPropertyBinder>();

        private readonly IEnumerable<IConverterFamily> _families;
        private readonly List<IConverterFamily> _defaultFamilies = new List<IConverterFamily>{
                new ExpandEnvironmentVariablesFamily(),
                new ResolveConnectionStringFamily(),
                new BooleanFamily(),
                new NumericTypeFamily(),
                new ObjectTypeFamily()
        };

        public BindingRegistry() : this(new ConverterLibrary(), Enumerable.Empty<IModelBinder>(), Enumerable.Empty<IPropertyBinder>(), Enumerable.Empty<IConverterFamily>())
        {
        }

        public BindingRegistry(ConverterLibrary converters, IEnumerable<IModelBinder> binders, IEnumerable<IPropertyBinder> propertyBinders, IEnumerable<IConverterFamily> converterFamilies)
        {
            configureModelBinders(binders);
            configurePropertyBinders(propertyBinders);

            _families = converterFamilies;

            Converters = converters;
            _defaultFamilies.Add(new BasicConverterFamily(Converters));
        }

        private void configurePropertyBinders(IEnumerable<IPropertyBinder> propertyBinders)
        {
            _propertyBinders.AddRange(propertyBinders);
            _propertyBinderCache.OnMissing = prop => AllPropertyBinders().FirstOrDefault(x => x.Matches(prop));

            _defaultPropertyBinders = new Lazy<IEnumerable<IPropertyBinder>>(buildDefaultPropertyBinders);
        }

        private void configureModelBinders(IEnumerable<IModelBinder> binders)
        {
            _modelBinderCache.OnMissing = type => AllModelBinders().FirstOrDefault(x => x.Matches(type));
            _standardBinder = new Lazy<StandardModelBinder>(buildStandardModelBinder);

            // DO NOT put the standard model binder at top
            _binders.AddRange(binders.Where(x => !(x is StandardModelBinder)));
        }

        private IEnumerable<IPropertyBinder> buildDefaultPropertyBinders()
        {
            yield return new AttributePropertyBinder();
            yield return new CollectionPropertyBinder(new DefaultCollectionTypeProvider());
            yield return new ConversionPropertyBinder(this);
            yield return new NestedObjectPropertyBinder();
        }

        private StandardModelBinder buildStandardModelBinder()
        {
            return new StandardModelBinder(this, new TypeDescriptorCache());
        }

        public void AddModelBinder(IModelBinder binder)
        {
            _binders.Add(binder);
        }

        public IEnumerable<IModelBinder> AllModelBinders()
        {
            foreach (var binder in _binders)
            {
                yield return binder;
            }

            yield return _standardBinder.Value;
        }

        public IEnumerable<IPropertyBinder> AllPropertyBinders()
        {
            foreach (var propertyBinder in _propertyBinders)
            {
                yield return propertyBinder;
            }

            foreach (var propertyBinder in _defaultPropertyBinders.Value)
            {
                yield return propertyBinder;
            }
        }

        public IEnumerable<IConverterFamily> AllConverterFamilies()
        {
            foreach (var family in _families)
            {
                yield return family;
            }

            foreach (var family in _defaultFamilies)
            {
                yield return family;
            }
        }

        public ConverterLibrary Converters { private set; get; }

        public IModelBinder BinderFor(Type modelType)
        {
            return _modelBinderCache[modelType];
        }

        public ValueConverter FindConverter(PropertyInfo property)
        {
            var family = AllConverterFamilies().FirstOrDefault(x => x.Matches(property));
            return family == null ? null : family.Build(this, property);
        }

        public IPropertyBinder BinderFor(PropertyInfo property)
        {
            return _propertyBinderCache[property];
        }
    }
}