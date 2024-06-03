// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using static AudioStreamSupportEditor.AudioStreamSupportEditor;

namespace AudioStreamSupportEditor
{
    public abstract class AudioStreamCustomInspectorBase : Editor
    {
        protected virtual void SetFieldsConditions()
        {
            // . custom inspector is sometimes buggily invoked for different base class what
            if (target == null)
                return;
        }

        protected List<EnumFieldCondition> enumFieldConditions;
        protected List<BoolFieldCondition> boolFieldConditions;
        protected List<TypeOfTargetCondition> typeOfTargetConditions;
        protected List<StringStartsWithFieldCondition> stringFieldStartsWithConditions;
        public virtual void OnEnable()
        {
            this.enumFieldConditions = new List<EnumFieldCondition>();
            this.boolFieldConditions = new List<BoolFieldCondition>();
            this.typeOfTargetConditions = new List<TypeOfTargetCondition>();
            this.stringFieldStartsWithConditions = new List<StringStartsWithFieldCondition>();
            this.SetFieldsConditions();
        }
        public override void OnInspectorGUI()
        {
            // Update the serializedProperty - always do this in the beginning of OnInspectorGUI.
            serializedObject.Update();

            var obj = serializedObject.GetIterator();

            if (obj.NextVisible(true))
            {
                // Loops through all visible fields
                do
                {
                    bool shouldBeVisible = true;
                    {
                        // Tests if the field is a field that should be hidden/shown based on other's enum value
                        foreach (var fieldCondition in enumFieldConditions)
                        {
                            //If the fieldcondition isn't valid, display an error msg.
                            if (!fieldCondition.isValid)
                            {
                                Debug.LogError(fieldCondition.errorMsg);
                            }
                            else if (fieldCondition.targetFieldName == obj.name)
                            {
                                var conditionEnumValue = (System.Enum)fieldCondition.conditionFieldValue;
                                var currentEnumValue = (System.Enum)target.GetType().GetField(fieldCondition.conditionFieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);

                                // If the enum value isn't equal to the wanted value the field will be set not to show for non negative condition
                                if (
                                    (
                                        (!fieldCondition.isNegative && currentEnumValue.ToString() != conditionEnumValue.ToString())
                                        || (fieldCondition.isNegative && currentEnumValue.ToString() == conditionEnumValue.ToString())
                                    )
                                    ||
                                    (
                                        fieldCondition.applicableForTypes != null
                                        && !fieldCondition.applicableForTypes.Contains(target.GetType())
                                    )
                                    )
                                {
                                    shouldBeVisible = false;
                                    break;
                                }
                            }
                        }
                    }

                    // if not precessed
                    if (shouldBeVisible)
                    {
                        // Tests if the field is a field that should be hidden/shown based on other's bool value
                        foreach (var fieldCondition in boolFieldConditions)
                        {
                            //If the fieldcondition isn't valid, display an error msg.
                            if (!fieldCondition.isValid)
                            {
                                Debug.LogError(fieldCondition.errorMsg);
                            }
                            else if (fieldCondition.targetFieldName == obj.name)
                            {
                                var boolField = target.GetType().GetField(fieldCondition.conditionFieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                var boolValue = (bool)boolField.GetValue(target);

                                //If the bool value isn't equal to the wanted value the field will be set not to show
                                if (boolValue != fieldCondition.conditionFieldValue
                                    || (fieldCondition.applicableForTypes != null
                                    && !fieldCondition.applicableForTypes.Contains(target.GetType()))
                                    )
                                {
                                    shouldBeVisible = false;
                                    break;
                                }
                            }
                        }
                    }

                    if (shouldBeVisible)
                    {
                        // Tests if the field is a field that should be hidden/shown based on target type
                        foreach (var typeOfTargetCondition in typeOfTargetConditions)
                        {
                            //If the fieldcondition isn't valid, display an error msg.
                            if (!typeOfTargetCondition.isValid)
                            {
                                Debug.LogError(typeOfTargetCondition.errorMsg);
                            }
                            else if (typeOfTargetCondition.targetFieldName == obj.name)
                            {
                                var targetType = target.GetType();

                                if (!typeOfTargetCondition.applicableForTypes.Contains(targetType))
                                {
                                    shouldBeVisible = false;
                                    break;
                                }
                            }
                        }
                    }

                    if (shouldBeVisible)
                    {
                        foreach (var fieldCondition in this.stringFieldStartsWithConditions)
                        {
                            //If the fieldcondition isn't valid, display an error msg.
                            if (!fieldCondition.isValid)
                            {
                                Debug.LogError(fieldCondition.errorMsg);
                            }
                            else if (fieldCondition.targetFieldName == obj.name)
                            {
                                var field = target.GetType().GetField(fieldCondition.conditionFieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                var fieldValue = (string)field.GetValue(target);

                                if (!fieldValue.StartsWith(fieldCondition.conditionFieldValueStartsWith)
                                    || (fieldCondition.applicableForTypes != null
                                    && !fieldCondition.applicableForTypes.Contains(target.GetType()))
                                    )
                                {
                                    shouldBeVisible = false;
                                    break;
                                }
                            }
                        }
                    }

                    if (shouldBeVisible)
                        EditorGUILayout.PropertyField(obj, true);

                } while (obj.NextVisible(false));
            }

            // Apply changes to the serializedProperty - always do this in the end of OnInspectorGUI.
            serializedObject.ApplyModifiedProperties();
        }
    }
}