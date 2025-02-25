﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using XrmToolBox.Extensibility;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk;
using McTools.Xrm.Connection;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Syncfusion.Windows.Forms.Tools;
using System.IO;
using System.Diagnostics;

namespace AlbanianXrm.EarlyBound
{
    public partial class MyPluginControl : PluginControlBase
    {
        private Settings mySettings;
        private TreeViewAdvBeforeCheckEventHandler treeEventHandler;
        private Factories.MyPluginFactory MyPluginFactory;
        private Logic.EntityMetadataHandler EntityMetadataHandler;
        private Logic.AttributeMetadataHandler AttributeMetadataHandler;
        private Logic.RelationshipMetadataHandler RelationshipMetadataHandler;
        private Logic.CoreToolsDownloader CoreToolsDownloader;
        private Logic.EntityGeneratorHandler EntityGeneratorHandler;

        public MyPluginControl()
        {
            InitializeComponent();
            MyPluginFactory = new Factories.MyPluginFactory();
            AttributeMetadataHandler = MyPluginFactory.NewAttributeMetadataHandler(this);
            CoreToolsDownloader = MyPluginFactory.NewCoreToolsDownloader(this);
            EntityGeneratorHandler = MyPluginFactory.NewEntityGeneratorHandler(this, metadataTree);
            EntityMetadataHandler = MyPluginFactory.NewEntityMetadataHandler(this, metadataTree);
            RelationshipMetadataHandler = MyPluginFactory.NewRelationshipMetadataHandler(this);
            treeEventHandler = new TreeViewAdvBeforeCheckEventHandler(this.MetadataTree_BeforeCheck);
            this.metadataTree.BeforeCheck += treeEventHandler;
        }

        private void MyPluginControl_Load(object sender, EventArgs e)
        {
            ShowInfoNotification("To contribute to this plugin visit its code repository", new Uri("https://github.com/Albanian-Xrm/Early-Bound"));

            // Loads or creates the settings for the plugin
            if (!SettingsManager.Instance.TryLoad(GetType(), out mySettings))
            {
                mySettings = new Settings();
                LogWarning("Settings not found => a new settings file has been created!");
            }
            else
            {
                LogInfo("Settings found and loaded");
                txtNamespace.Text = mySettings.Namespace;
                txtOutputPath.Text = mySettings.OutputPath;
            }
        }

        /// <summary>
        /// This event occurs when the plugin is closed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MyPluginControl_OnCloseTool(object sender, EventArgs e)
        {
            // Before leaving, save the settings
            LogInfo("Saving current settings");
            SettingsManager.Instance.Save(GetType(), mySettings);
        }

        /// <summary>
        /// This event occurs when the connection has been updated in XrmToolBox
        /// </summary>
        public override void UpdateConnection(IOrganizationService newService, ConnectionDetail detail, string actionName, object parameter)
        {
            base.UpdateConnection(newService, detail, actionName, parameter);

            if (mySettings != null && detail != null)
            {
                LogInfo("Connection has changed to: {0}", detail.WebApplicationUrl);
            }
        }

        private void BtnGetMetadata_Click(object sender, EventArgs e)
        {
            EntityMetadataHandler.GetEntityList();
        }

        private void TreeViewAdv1_BeforeExpand(object sender, TreeViewAdvCancelableNodeEventArgs e)
        {
            if (e.Node.ExpandedOnce) return;
            metadataTree.BeginUpdate();
            var metadata = (EntityMetadata)e.Node.Parent.Tag;
            if (e.Node.Text == "Attributes")
            {
                AttributeMetadataHandler.GetAttributes(metadata.LogicalName, e.Node);
            }
            else if (e.Node.Text == "Relationships")
            {
                RelationshipMetadataHandler.GetRelationships(metadata.LogicalName, e.Node);
            }
            metadataTree.EndUpdate(true);
        }

        private void BtnCoreTools_Click(object sender, EventArgs e)
        {
            CoreToolsDownloader.DownloadCoreTools();
        }

        private void BtnGenerateEntities_Click(object sender, EventArgs e)
        {
            EntityGeneratorHandler.GenerateEntities(txtNamespace.Text, txtOutputPath.Text);
        }

        private void TxtOutputPath_TextChanged(object sender, EventArgs e)
        {
            mySettings.OutputPath = txtOutputPath.Text;
        }

        private void TxtNamespace_TextChanged(object sender, EventArgs e)
        {
            mySettings.Namespace = txtNamespace.Text;
        }

        private void ToolStripButton4_Click(object sender, EventArgs e)
        {
            LogInfo("Saving current settings");
            SettingsManager.Instance.Save(GetType(), mySettings);
        }

        private void MetadataTree_BeforeCheck(object sender, TreeNodeAdvBeforeCheckEventArgs e)
        {

            if (e.Node.Tag is RelationshipMetadataBase)
            {
                string entity1;
                string entity2;
                string schemaName;
                MessageBox.Show($"{sender}");
                if (e.Node.Tag is OneToManyRelationshipMetadata)
                {
                    var metadata = (OneToManyRelationshipMetadata)e.Node.Tag;
                    entity1 = metadata.ReferencingEntity;
                    entity2 = metadata.ReferencedEntity;
                    schemaName = metadata.SchemaName;
                }
                else
                {
                    var metadata = (ManyToManyRelationshipMetadata)e.Node.Tag;
                    entity1 = metadata.Entity1LogicalName;
                    entity2 = metadata.Entity2LogicalName;
                    schemaName = metadata.SchemaName;
                }

                MessageBox.Show($"{schemaName} {entity1} {entity2}");

                foreach (TreeNodeAdv entity in metadataTree.Nodes)
                {
                    var entityName = ((EntityMetadata)entity.Tag).LogicalName;
                    if (entityName == entity1 || entityName == entity2)
                    {
                        foreach (TreeNodeAdv item in entity.Nodes)
                        {
                            if (item.Text == "Relationships")
                            {
                                if (!item.ExpandedOnce)
                                {
                                    WorkAsync(new WorkAsyncInfo
                                    {
                                        Message = $"Getting relationships for entity {entityName}",
                                        Work = (worker, args) =>
                                        {
                                            args.Result = Service.Execute(new RetrieveEntityRequest()
                                            {
                                                EntityFilters = EntityFilters.Relationships,
                                                LogicalName = entityName
                                            });
                                        },
                                        PostWorkCallBack = (args) =>
                                        {
                                            if (args.Error != null)
                                            {
                                                MessageBox.Show(args.Error.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                            }
                                            var result = args.Result as RetrieveEntityResponse;
                                            if (result != null)
                                            {
                                                item.ExpandedOnce = true;
                                                foreach (var relationship in result.EntityMetadata.ManyToManyRelationships.Union<RelationshipMetadataBase>(
                                                                     result.EntityMetadata.OneToManyRelationships).Union(
                                                                     result.EntityMetadata.ManyToOneRelationships).OrderBy(x => x.SchemaName))
                                                {
                                                    TreeNodeAdv node = new TreeNodeAdv($"{relationship.SchemaName}")
                                                    {
                                                        ExpandedOnce = true,
                                                        ShowCheckBox = true,
                                                        Tag = relationship
                                                    };
                                                    if (schemaName == relationship.SchemaName)
                                                    {
                                                        node.CheckState = e.NewCheckState;
                                                    }
                                                    item.Nodes.Add(node);
                                                }
                                            }
                                        }
                                    });
                                }
                                else
                                {
                                    foreach (TreeNodeAdv relationship in item.Nodes)
                                    {
                                        if (relationship == e.Node) continue;
                                        if (((RelationshipMetadataBase)relationship.Tag).SchemaName == schemaName)
                                        {
                                            MessageBox.Show($"{schemaName} {entityName}");
                                            this.metadataTree.BeforeCheck -= treeEventHandler;
                                            relationship.CheckState = e.NewCheckState;
                                            treeEventHandler = new TreeViewAdvBeforeCheckEventHandler(this.MetadataTree_BeforeCheck);
                                            this.metadataTree.BeforeCheck += treeEventHandler;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}