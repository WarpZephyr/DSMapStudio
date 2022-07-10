﻿using System;
using System.Collections.Generic;
using System.Text;
using Veldrid;
using ImGuiNET;

namespace StudioCore.MsbEditor
{
    public class SearchProperties
    {
        private Universe Universe = null;
        public string PropertyName = "";
        private object PropertyValue = null;
        private Type PropertyType = null;
        private bool ValidType = false;

        private Dictionary<string, List<WeakReference<Entity>>> FoundObjects = new Dictionary<string, List<WeakReference<Entity>>>();

        public SearchProperties(Universe universe)
        {
            Universe = universe;
        }

        public bool InitializeSearchValue()
        {
            if (PropertyType == typeof(bool))
            {
                PropertyValue = false;
                return true;
            }
            else if (PropertyType == typeof(byte))
            {
                PropertyValue = (byte)0;
                return true;
            }
            else if (PropertyType == typeof(char))
            {
                PropertyValue = (char)0;
                return true;
            }
            else if (PropertyType == typeof(short))
            {
                PropertyValue = (short)0;
                return true;
            }
            else if (PropertyType == typeof(ushort))
            {
                PropertyValue = (ushort)0;
                return true;
            }
            else if (PropertyType == typeof(int))
            {
                PropertyValue = (int)0;
                return true;
            }
            else if (PropertyType == typeof(uint))
            {
                PropertyValue = (uint)0;
                return true;
            }
            else if (PropertyType == typeof(long))
            {
                PropertyValue = (long)0;
                return true;
            }
            else if (PropertyType == typeof(ulong))
            {
                PropertyValue = (ulong)0;
                return true;
            }
            else if (PropertyType == typeof(float))
            {
                PropertyValue = 0.0f;
                return true;
            }
            else if (PropertyType == typeof(double))
            {
                PropertyValue = 0.0d;
                return true;
            }
            else if (PropertyType == typeof(string))
            {
                PropertyValue = "";
                return true;
            }
            return false;
        }

        public bool SearchValue()
        {
            ImGui.Text("Value (Exact)");
            ImGui.NextColumn();
            bool ret = false;
            if (PropertyType == typeof(bool))
            {
                var val = (bool)PropertyValue;
                if (ImGui.Checkbox("##valBool", ref val))
                {
                    PropertyValue = val;
                    ret = true;
                }
            }
            else if (PropertyType == typeof(byte))
            {
                var val = (int)(byte)PropertyValue;

                if (ImGui.InputInt("##value4", ref val))
                {
                    PropertyValue = (byte)val;
                    ret = true;
                }
            }
            else if (PropertyType == typeof(char))
            {
                var val = (int)(char)PropertyValue;

                if (ImGui.InputInt("##value4", ref val))
                {
                    PropertyValue = (char)val;
                    ret = true;
                }
            }
            else if (PropertyType == typeof(short))
            {
                var val = (int)(short)PropertyValue;

                if (ImGui.InputInt("##value4", ref val))
                {
                    PropertyValue = (short)val;
                    ret = true;
                }
            }
            else if (PropertyType == typeof(ushort))
            {
                var val = (int)(ushort)PropertyValue;

                if (ImGui.InputInt("##value4", ref val))
                {
                    PropertyValue = (ushort)val;
                    ret = true;
                }
            }
            else if (PropertyType == typeof(int))
            {
                int ival = (int)PropertyValue;
                if (ImGui.InputInt("##value2", ref ival))
                {
                    PropertyValue = ival;
                    ret = true;
                }
            }
            else if (PropertyType == typeof(uint))
            {
                var val = (int)(uint)PropertyValue;

                if (ImGui.InputInt("##value4", ref val))
                {
                    PropertyValue = (uint)val;
                    ret = true;
                }
            }
            else if (PropertyType == typeof(long))
            {
                var val = (int)(long)PropertyValue;

                if (ImGui.InputInt("##value4", ref val))
                {
                    PropertyValue = (long)val;
                    ret = true;
                }
            }
            else if (PropertyType == typeof(ulong))
            {
                var val = (int)(ulong)PropertyValue;

                if (ImGui.InputInt("##value4", ref val))
                {
                    PropertyValue = (ulong)val;
                    ret = true;
                }
            }
            else if (PropertyType == typeof(float))
            {
                var val = (float)PropertyValue;
                if (ImGui.InputFloat("##value3", ref val))
                {
                    PropertyValue = val;
                    ret = true;
                }
            }
            else if (PropertyType == typeof(double))
            {
                var val = (double)PropertyValue;
                if (ImGui.InputDouble("##value3", ref val))
                {
                    PropertyValue = val;
                    ret = true;
                }
            }
            else if (PropertyType == typeof(string))
            {
                string val = (string)PropertyValue;
                if (val == null)
                {
                    val = "";
                }
                if (ImGui.InputText("##value2", ref val, 99))
                {
                    PropertyValue = val;
                    ret = true;
                }
            }
            ImGui.NextColumn();
            return ret;
        }

        public void OnGui(string propname=null)
        {
            if (propname != null)
            {
                ImGui.SetNextWindowFocus();
                PropertyName = propname;
                PropertyType = Universe.GetPropertyType(PropertyName);
                ValidType = InitializeSearchValue();
            }
            if (ImGui.Begin("Search Properties"))
            {
                ImGui.Text("Search Properties By Name <Ctrl+F>");
                ImGui.Separator();
                ImGui.Columns(2);
                ImGui.Text("Property Name");
                ImGui.NextColumn();

                if (InputTracker.GetControlShortcut(Key.F))
                    ImGui.SetKeyboardFocusHere();
                if (ImGui.InputText("##value", ref PropertyName, 64))
                {
                    PropertyType = Universe.GetPropertyType(PropertyName);
                    ValidType = InitializeSearchValue();
                }
                ImGui.NextColumn();
                if (PropertyType != null && ValidType)
                {
                    ImGui.Text("Type");
                    ImGui.NextColumn();
                    ImGui.Text(PropertyType.Name);
                    ImGui.NextColumn();
                    if (SearchValue())
                    {
                        FoundObjects.Clear();
                        foreach (var o in Universe.LoadedObjectContainers.Values)
                        {
                            if (o == null)
                            {
                                continue;
                            }
                            if (o is Map m)
                            {
                                foreach (var ob in m.Objects)
                                {
                                    if (ob is MapEntity e)
                                    {
                                        var p = ob.GetPropertyValue(PropertyName);
                                        if (p != null && p.Equals(PropertyValue))
                                        {
                                            if (!FoundObjects.ContainsKey(e.ContainingMap.Name))
                                            {
                                                FoundObjects.Add(e.ContainingMap.Name, new List<WeakReference<Entity>>());
                                            }
                                            FoundObjects[e.ContainingMap.Name].Add(new WeakReference<Entity>(e));
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                ImGui.Columns(1);
                if (FoundObjects.Count > 0)
                {
                    ImGui.Text("Search Results");
                    ImGui.Separator();
                    ImGui.BeginChild("Search Results");
                    foreach (var f in FoundObjects)
                    {
                        if (ImGui.TreeNodeEx(f.Key, ImGuiTreeNodeFlags.DefaultOpen))
                        {
                            foreach (var o in f.Value)
                            {
                                Entity obj;
                                if (o.TryGetTarget(out obj))
                                {
                                    if (ImGui.Selectable(obj.Name, Universe.Selection.GetSelection().Contains(obj), ImGuiSelectableFlags.AllowDoubleClick))
                                    {
                                        if (InputTracker.GetKey(Key.ControlLeft) || InputTracker.GetKey(Key.ControlRight))
                                        {
                                            Universe.Selection.AddSelection(obj);
                                        }
                                        else
                                        {
                                            Universe.Selection.ClearSelection();
                                            Universe.Selection.AddSelection(obj);
                                        }
                                    }
                                }
                            }
                            ImGui.TreePop();
                        }
                    }
                    ImGui.EndChild();
                }
            }
            ImGui.End();
        }
    }
}
