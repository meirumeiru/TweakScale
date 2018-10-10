﻿using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

namespace TweakScale
{
    public class MemberUpdater
    {
        private readonly object _object;
        private readonly FieldInfo _field;
        private readonly PropertyInfo _property;
        private readonly UI_FloatRange _floatRange;
        private const BindingFlags LookupFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        static void ConcatSafely(string name, Func<string> a, ref string result)
        {
            try
            {
                result += ' ' + name + " = ";
                result += a() + ' ';
            }
            catch
            {
                result += "EXCEPTION";
            }
        }

        public static MemberUpdater Create(object obj, string name)
        {
            if (obj == null || name == null)
            {
                return null;
            }
			Type objectType = obj.GetType();
			FieldInfo field = objectType.GetField(name, LookupFlags);
			PropertyInfo property = objectType.GetProperty(name, LookupFlags);
            UI_FloatRange floatRange = null;
            BaseFieldList fields;
            if (obj is PartModule)
            {
                fields = (obj as PartModule).Fields;
                try
				{
					BaseField fieldData = fields[name];
                    if ((object)fieldData != null)
					{
						UI_Control ctrl = fieldData.uiControlEditor;
                        if (ctrl is UI_FloatRange)
                        {
                            floatRange = ctrl as UI_FloatRange;
                        }
                    }
                }
                catch (Exception)
                {
                    string debuglog = "===============================================\r\n";
                    ConcatSafely("objectType", () => { return objectType.ToString(); }, ref debuglog);
                    ConcatSafely("name", () => { return name; }, ref debuglog);
                    ConcatSafely("fields.ReflectType", () => { return fields.ReflectedType.ToString().ToString(); }, ref debuglog);
                    ConcatSafely("fields.Count", () => { return fields.Count.ToString(); }, ref debuglog);
                    ConcatSafely("fields.Count", 
                        () => 
                        {
                            string acc = "";
                            int numFields = fields.Count;
                            for (int i = 0; i < numFields; i++)
                            {
                                ConcatSafely("field", () => { return fields[i].ToString_rec(1); }, ref acc);
                            }
                            return acc;
                        }, ref debuglog);
                    Tools.LogWf(debuglog);
                }
            }

            if (property != null && property.GetIndexParameters().Length > 0)
            {
                Tools.LogWf("Property {0} on {1} requires indices, which TweakScale currently does not support.", name, objectType.Name);
                return null;
            }
            if (field == null && property == null)
            {
                // handle special cases
                if ((obj is PartModule) && (name == "inputResources" || name == "outputResources"))
                    return Create((obj as PartModule).resHandler, name);

                Tools.LogWf("No valid member found for {0} in {1}", name, objectType.Name);

                return null;
            }

            return new MemberUpdater(obj, field, property, floatRange);
        }

        private MemberUpdater(object obj, FieldInfo field, PropertyInfo property, UI_FloatRange floatRange)
        {
            _object = obj;
            _field = field;
            _property = property;
            _floatRange = floatRange;
        }

        public object Value
        {
            get
            {
                if (_field != null)
                {
                    return _field.GetValue(_object);
                }
                if (_property != null)
                {
                    return _property.GetValue(_object, null);
                }
                return null;
            }
        }

        public Type MemberType
        {
            get
            {
                if (_field != null)
                {
                    return _field.FieldType;
                }
                if (_property != null)
                {
                    return _property.PropertyType;
                }
                return null;
            }
        }

        public void Set(object value)
        {
            if (value.GetType() != MemberType && MemberType.GetInterface("IConvertible") != null &&
                value.GetType().GetInterface("IConvertible") != null)
            {
                value = Convert.ChangeType(value, MemberType);
            }

            if (_field != null)
            {
                _field.SetValue(_object, value);
            }
            else if (_property != null)
            {
                _property.SetValue(_object, value, null);
            }
        }

        public void Scale(double scale, MemberUpdater source)
        {
            if (_field == null && _property == null)
            {
                return;
            }

			object newValue = source.Value;
			if (MemberType == typeof(float))
			{
				RescaleFloatRange((float)scale);
				Set((float)newValue * (float)scale);
			}
			else if (MemberType == typeof(double))
			{
				RescaleFloatRange((float)scale);
				Set((double)newValue * scale);
			}
			else if (MemberType == typeof(int))
			{
				Set((int)Math.Round((int)(newValue) * scale));
			}
			else if (MemberType == typeof(Vector3))
			{
				Set((Vector3)newValue * (float)scale);
			}
			else if (MemberType == typeof(FloatCurve))
			{
				AnimationCurve curve = (newValue as FloatCurve).Curve;
				AnimationCurve tmp = new AnimationCurve();
				for (int i = 0; i < curve.length; i++)
				{
					Keyframe k = curve.keys[i];
					k.value *= (float)scale;
					k.inTangent *= (float)scale;
					k.outTangent *= (float)scale;
					tmp.AddKey(k);
				}
				(Value as FloatCurve).Curve = tmp;
			}
			else if (MemberType == typeof(List<ResourceRatio>))
			{
				List<ResourceRatio> l = (newValue as List<ResourceRatio>);
				//List<ResourceRatio> l2 = new List<ResourceRatio>();
				for (int i = 0; i < l.Count; i++)
				{
					ResourceRatio tmp = l[i];
					tmp.Ratio *= scale;
					(Value as List<ResourceRatio>)[i] = tmp;
				}
			}
			else if (MemberType == typeof(ConversionRecipe))
			{
				ConversionRecipe l = (newValue as ConversionRecipe);
                ScaleResourceList(l.Inputs, scale);
                ScaleResourceList(l.Outputs, scale);
                ScaleResourceList(l.Requirements, scale);
            }
        }

        public void ScaleResourceList(List<ResourceRatio> l, double scale)
        {
            for (int i = 0; i < l.Count; i++)
			{
				ResourceRatio tmp = l[i];
                tmp.Ratio *= scale;
                l[i] = tmp;
            }
        }

        private void RescaleFloatRange(float factor)
        {
            if ((object)_floatRange == null)
            {
                return;
            }
            _floatRange.maxValue *= factor;
            _floatRange.minValue *= factor;
            _floatRange.stepIncrement *= factor;
        }

        public string Name {
            get
            {
                if (_field != null)
                {
                    return _field.Name;
                }
                if (_property != null)
                {
                    return _property.Name;
                }
                return null;
            }
        }
    }
}
