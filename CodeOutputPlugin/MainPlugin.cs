﻿using CodeOutputPlugin.Manager;
using Gum;
using Gum.DataTypes;
using Gum.DataTypes.Variables;
using Gum.Plugins;
using Gum.Plugins.BaseClasses;
using Gum.ToolStates;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using ToolsUtilities;

namespace CodeOutputPlugin
{
    [Export(typeof(PluginBase))]
    public class MainPlugin : PluginBase
    {
        #region Fields/Properties

        public override string FriendlyName => "Code Output Plugin";

        public override Version Version => new Version(1, 0);

        Views.CodeWindow control;
        ViewModels.CodeWindowViewModel viewModel;
        Models.CodeOutputProjectSettings codeOutputProjectSettings;

        #endregion

        public override bool ShutDown(PluginShutDownReason shutDownReason)
        {
            return true;
        }

        public override void StartUp()
        {
            AssignEvents();

            var item = this.AddMenuItem("Plugins", "View Code");
            item.Click += HandleViewCodeClicked;

            if (control == null)
            {
                CreateControl();
            }
        }

        private void AssignEvents()
        {
            this.InstanceSelected += HandleInstanceSelected;
            this.InstanceDelete += HandleInstanceDeleted;
            this.InstanceAdd += HandleInstanceAdd;
            this.InstanceReordered += HandleInstanceReordered;

            this.ElementSelected += HandleElementSelected;

            this.VariableAdd += HandleVariableAdd;
            this.VariableSet += HandleVariableSet;
            this.VariableDelete += HandleVariableDelete;


            this.StateWindowTreeNodeSelected += HandleStateSelected;
            this.StateRename += HandleStateRename;
            this.StateAdd += HandleStateAdd;
            this.StateDelete += HandleStateDelete;

            this.CategoryRename += (category,newName) => HandleRefreshAndExport();
            this.CategoryAdd += (category) => HandleRefreshAndExport();
            this.CategoryDelete += (category) => HandleRefreshAndExport();
            this.VariableRemovedFromCategory += (name, category) => HandleRefreshAndExport();

            this.AddAndRemoveVariablesForType += CustomVariableManager.HandleAddAndRemoveVariablesForType;
            this.ProjectLoad += HandleProjectLoaded;
        }


        private void HandleProjectLoaded(GumProjectSave project)
        {
            codeOutputProjectSettings = CodeOutputProjectSettingsManager.CreateOrLoadSettingsForProject();
            viewModel.IsCodeGenPluginEnabled = codeOutputProjectSettings.IsCodeGenPluginEnabled;
            HandleElementSelected(null);
        }

        private void HandleStateSelected(TreeNode obj)
        {
            if (control != null)
            {
                LoadCodeSettingsFile();

                RefreshCodeDisplay();
            }
        }

        private void HandleInstanceSelected(ElementSave arg1, InstanceSave instance)
        {
            if(control != null)
            {
                LoadCodeSettingsFile();

                RefreshCodeDisplay();
            }
        }

        private void HandleElementSelected(ElementSave element)
        {
            if (control != null)
            {
                LoadCodeSettingsFile();

                RefreshCodeDisplay();
            }
        }

        private void LoadCodeSettingsFile()
        {
            var element = SelectedState.Self.SelectedElement;
            if(element != null)
            {
                control.CodeOutputElementSettings = CodeOutputElementSettingsManager.LoadOrCreateSettingsFor(element);
            }
            else
            {
                control.CodeOutputElementSettings = new Models.CodeOutputElementSettings();
            }
        }

        private void HandleVariableSet(ElementSave element, InstanceSave instance, string arg3, object arg4) => HandleRefreshAndExport();
        private void HandleVariableAdd(ElementSave elementSave, string variableName) => HandleRefreshAndExport();
        //private void /*/*HandleVariableRemoved*/*/(ElementSave elementSave, string variableName) => HandleRefreshAndExport();
        private void HandleVariableDelete(ElementSave arg1, string arg2) => HandleRefreshAndExport();

        private void HandleStateRename(StateSave arg1, string arg2) => HandleRefreshAndExport();
        private void HandleStateAdd(StateSave obj) => HandleRefreshAndExport();
        private void HandleStateDelete(StateSave obj) => HandleRefreshAndExport();

        private void HandleInstanceDeleted(ElementSave arg1, InstanceSave arg2) => HandleRefreshAndExport();
        private void HandleInstanceAdd(ElementSave arg1, InstanceSave arg2) => HandleRefreshAndExport();
        private void HandleInstanceReordered(InstanceSave obj) => HandleRefreshAndExport();


        private void HandleRefreshAndExport()
        {
            if (control != null)
            {
                RefreshCodeDisplay();

                if (control.CodeOutputElementSettings == null)
                {
                    control.CodeOutputElementSettings = new Models.CodeOutputElementSettings();
                }

                var elementSettings = control.CodeOutputElementSettings;

                if (elementSettings.AutoGenerateOnChange)
                {
                    GenerateCodeForSelectedElement(showPopups: false);
                }
            }
        }

        private void HandleViewCodeClicked(object sender, EventArgs e)
        {
            //GumCommands.Self.GuiCommands.ShowControl(control);

            LoadCodeSettingsFile();

            RefreshCodeDisplay();

        }


        private void RefreshCodeDisplay()
        {
            var instance = SelectedState.Self.SelectedInstance;
            var selectedElement = SelectedState.Self.SelectedElement;

            control.CodeOutputProjectSettings = codeOutputProjectSettings;
            if(control.CodeOutputElementSettings == null)
            {
                control.CodeOutputElementSettings = new Models.CodeOutputElementSettings();
            }

            var settings = control.CodeOutputElementSettings;

            if(settings.GenerationBehavior != Models.GenerationBehavior.NeverGenerate)
            {
                switch(viewModel.WhatToView)
                {
                    case ViewModels.WhatToView.SelectedElement:

                        if (instance != null)
                        {
                            string code = CodeGenerator.GetCodeForInstance(instance, selectedElement, CodeGenerator.GetVisualApiForInstance(instance, selectedElement) );
                            viewModel.Code = code;
                        }
                        else if(selectedElement != null)
                        {

                            string gumCode = CodeGenerator.GetGeneratedCodeForElement(selectedElement, settings, codeOutputProjectSettings);
                            viewModel.Code = $"//Code for {selectedElement.ToString()}\n{gumCode}";
                        }
                        break;
                    case ViewModels.WhatToView.SelectedState:
                        var state = SelectedState.Self.SelectedStateSave;

                        if (state != null && selectedElement != null)
                        {
                            string gumCode = CodeGenerator.GetCodeForState(selectedElement, state, VisualApi.Gum);
                            viewModel.Code = $"//State Code for {state.Name ?? "Default"}:\n{gumCode}";
                        }
                        break;
                }
            }
            else if(selectedElement == null)
            {
                viewModel.Code = "// Select a Screen, Component, or Standard to see generated code";
            }
            else
            {
                viewModel.Code = "// code generation disabled for this object";
            }


        }

        private void CreateControl()
        {
            control = new Views.CodeWindow();
            viewModel = new ViewModels.CodeWindowViewModel();

            control.CodeOutputSettingsPropertyChanged += (not, used) => HandleCodeOutputPropertyChanged();
            control.GenerateCodeClicked += (not, used) => HandleGenerateCodeButtonClicked();
            control.GenerateAllCodeClicked += (not, used) => HandleGenerateAllCodeButtonClicked();
            viewModel.PropertyChanged += (sender, args) => HandleMainViewModelPropertyChanged(args.PropertyName);

            control.DataContext = viewModel;

            // We don't actually want it to show, just associate, so add and immediately remove.
            // Eventually we want this to be done with a single call but I don't know if there's Gum
            // support for it yet
            GumCommands.Self.GuiCommands.AddControl(control, "Code", TabLocation.RightBottom);
        }

        private void HandleMainViewModelPropertyChanged(string propertyName)
        {
            switch(propertyName)
            {
                case nameof(viewModel.IsCodeGenPluginEnabled):
                    codeOutputProjectSettings.IsCodeGenPluginEnabled = viewModel.IsCodeGenPluginEnabled;
                    CodeOutputProjectSettingsManager.WriteSettingsForProject(codeOutputProjectSettings);

                    break;
                default:
                    RefreshCodeDisplay();
                    break;
            }
        }

        private void HandleCodeOutputPropertyChanged()
        {
            var element = SelectedState.Self.SelectedElement;
            if(element != null)
            {
                CodeOutputElementSettingsManager.WriteSettingsForElement(element, control.CodeOutputElementSettings);

                RefreshCodeDisplay();
            }
            CodeOutputProjectSettingsManager.WriteSettingsForProject(codeOutputProjectSettings);
        }

        private void HandleGenerateCodeButtonClicked()
        {
            if(SelectedState.Self.SelectedElement != null)
            {
                GenerateCodeForSelectedElement(showPopups:true);
            }
        }

        private void HandleGenerateAllCodeButtonClicked()
        {
            var gumProject = GumState.Self.ProjectState.GumProjectSave;
            foreach (var screen in gumProject.Screens)
            {
                var screenOutputSettings = CodeOutputElementSettingsManager.LoadOrCreateSettingsFor(screen);
                GenerateCodeForElement(screen, screenOutputSettings, showPopups: false);
            }
            foreach(var component in gumProject.Components)
            {
                var componentOutputSettings = CodeOutputElementSettingsManager.LoadOrCreateSettingsFor(component);
                GenerateCodeForElement(component, componentOutputSettings, showPopups: false);
            }

            GumCommands.Self.GuiCommands.ShowMessage($"Generated code\nScreens: {gumProject.Screens.Count}\nComponents: {gumProject.Components.Count}");
        }

        private void GenerateCodeForSelectedElement(bool showPopups)
        {
            var selectedElement = SelectedState.Self.SelectedElement;
            var settings = control.CodeOutputElementSettings;
            GenerateCodeForElement(selectedElement, settings, showPopups);
        }

        private void GenerateCodeForElement(ElementSave selectedElement, Models.CodeOutputElementSettings elementSettings, bool showPopups)
        {
            string generatedFileName = elementSettings.GeneratedFileName;

            if (string.IsNullOrEmpty(generatedFileName) && !string.IsNullOrEmpty(this.codeOutputProjectSettings.CodeProjectRoot))
            {
                string prefix = selectedElement is ScreenSave ? "Screens"
                    : selectedElement is ComponentSave ? "Components"
                    : "Standards";
                var splitName = (prefix + "/" + selectedElement.Name).Split('/');
                var nameWithNamespaceArray = splitName.Take(splitName.Length - 1).Append(CodeGenerator.GetClassNameForType(selectedElement.Name, CodeGenerator.GetVisualApiForElement(selectedElement)));

                var folder = this.codeOutputProjectSettings.CodeProjectRoot;
                if (FileManager.IsRelative(folder))
                {
                    folder = GumState.Self.ProjectState.ProjectDirectory + folder;
                }

                generatedFileName = folder + string.Join("\\", nameWithNamespaceArray) + ".Generated.cs";
            }

            if (!string.IsNullOrEmpty(generatedFileName) && FileManager.IsRelative(generatedFileName))
            {
                generatedFileName = ProjectState.Self.ProjectDirectory + generatedFileName;
            }


            if (string.IsNullOrEmpty(generatedFileName))
            {
                if (showPopups)
                {
                    GumCommands.Self.GuiCommands.ShowMessage("Generated file name must be set first");
                }
            }
            else
            {
                // We used to use the view model code, but the viewmodel may have
                // an instance within the element selected. Instead, we want to output
                // the code for the whole selected element.
                //var contents = ViewModel.Code;

                string contents = CodeGenerator.GetGeneratedCodeForElement(selectedElement, elementSettings, codeOutputProjectSettings);
                contents = $"//Code for {selectedElement.ToString()}\n{contents}";

                string message = string.Empty;

                var codeDirectory = FileManager.GetDirectory(generatedFileName);
                if (!System.IO.Directory.Exists(codeDirectory))
                {
                    try
                    {
                        GumCommands.Self.TryMultipleTimes(() =>
                            System.IO.Directory.CreateDirectory(codeDirectory));
                    }
                    catch(Exception e)
                    {
                        GumCommands.Self.GuiCommands.PrintOutput($"Error creating directory {codeDirectory}:\n{e.Message}");
                    }
                }

                GumCommands.Self.TryMultipleTimes(() => System.IO.File.WriteAllText(generatedFileName, contents));

                // show a message somewhere?
                message += $"Generated code to {FileManager.RemovePath(generatedFileName)}";

                if (!string.IsNullOrEmpty(this.codeOutputProjectSettings.CodeProjectRoot))
                {
                    
                    if (FileManager.IsRelative(generatedFileName))
                    {
                        generatedFileName = GumState.Self.ProjectState.ProjectDirectory + generatedFileName;
                    }
                    generatedFileName = FileManager.RemoveDotDotSlash(generatedFileName);
                    var splitFileWithoutGenerated = generatedFileName.Split('.').ToArray();
                    var customCodeFileName = string.Join("\\", splitFileWithoutGenerated.Take(splitFileWithoutGenerated.Length - 2)) + ".cs";


                    // todo - only save this if it doesn't already exist
                    if (!System.IO.File.Exists(customCodeFileName))
                    {
                        var customCodeContents = CustomCodeGenerator.GetCustomCodeForElement(selectedElement, elementSettings, codeOutputProjectSettings);
                        System.IO.File.WriteAllText(customCodeFileName, customCodeContents);
                    }
                }


                if (showPopups)
                {
                    GumCommands.Self.GuiCommands.ShowMessage(message);
                }
                else
                {
                    GumCommands.Self.GuiCommands.PrintOutput(message);
                }
            }
        }
    }
}
