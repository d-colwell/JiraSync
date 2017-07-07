using JiraService;
using JiraService.Issue;
using JiraSync.ConfigEditor.ViewModels;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Tricentis.TCCore.CredentialManager;

namespace JiraSync.ConfigEditor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class ConfigEditor : Window
    {
        private Issue[] issues;
        private string[] toscaFields;
        public ConfigEditor()
        {
            if (!this.IsInitialized)
                InitializeComponent();
            DataContext = this;
            FieldMaps = new ObservableCollection<FieldMapViewModel>();
            expanderGrid.Visibility = Visibility.Collapsed;
            this.Activate();
        }

        public ConfigEditor(ConfigConstructor config) : this()
        {
            if (config.currentConfig == null)
            {
                config.currentConfig = new JiraService.Configuration.JiraConfig { fieldMaps = new List<JiraService.Configuration.FieldMap>() };
            }
            this.Config = new ViewModels.ConfigViewModel(config.currentConfig);
            this.toscaFields = config.availableAttributes.OrderBy(x => x).ToArray();
        }

        public ViewModels.ConfigViewModel Config
        {
            get { return (ViewModels.ConfigViewModel)GetValue(ConfigProperty); }
            set { SetValue(ConfigProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Config.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ConfigProperty =
            DependencyProperty.Register("Config", typeof(ViewModels.ConfigViewModel), typeof(ConfigEditor), new PropertyMetadata(null));

        public ObservableCollection<FieldMapViewModel> FieldMaps
        {
            get { return (ObservableCollection<FieldMapViewModel>)GetValue(FieldMapsProperty); }
            set { SetValue(FieldMapsProperty, value); }
        }

        // Using a DependencyProperty as the backing store for MyProperty.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty FieldMapsProperty =
            DependencyProperty.Register("FieldMaps", typeof(ObservableCollection<FieldMapViewModel>), typeof(ConfigEditor), new PropertyMetadata(null));




        public ObservableCollection<FieldMapViewModel> ToscaToJiraMaps
        {
            get { return (ObservableCollection<FieldMapViewModel>)GetValue(ToscaToJiraMapsProperty); }
            set { SetValue(ToscaToJiraMapsProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ToscaToJiraMaps.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ToscaToJiraMapsProperty =
            DependencyProperty.Register("ToscaToJiraMaps", typeof(ObservableCollection<FieldMapViewModel>), typeof(ConfigEditor), new PropertyMetadata(null));



        private void Button_Click(object sender, RoutedEventArgs e)
        {
            expanderGrid.Visibility = Visibility.Collapsed;
            errorMessage.Content = "";
            var credential = CredentialManager.Instance.Credentials.FirstOrDefault(x => x?.BaseURL == Config.BaseURL);
            if (credential == null)
            {
                errorMessage.Content = $"No credentials exists for that URL";
                return;
            }
            var jira = new Jira(Config.BaseURL, credential.Username, credential.Password);
            var issueService = jira.GetIssueService();
            var fieldService = jira.GetFieldService();
            try
            {
                errorMessage.Content = $"Attempting connection...";
                issues = issueService.Search(Config.JQLFilter);
                var fields = fieldService.GetFields();
                Dictionary<string, FieldMapViewModel> fieldMaps = new Dictionary<string, FieldMapViewModel>();
                foreach (var issue in issues)
                {
                    var properties = ((JObject)issue.InternalObject.SelectTokens("$").First()).Properties().Where(x => x.Value is JValue);
                    var fieldsContainer = issue.InternalObject.SelectToken("$.fields") as JObject;
                    var issueFields = fieldsContainer.Properties();
                    foreach (var prop in properties)
                    {
                        if (!fieldMaps.ContainsKey(prop.Path))

                            fieldMaps.Add(prop.Path, new FieldMapViewModel { DisplayedName = prop.Name, JiraJSONPath = prop.Path, Map = false, ToscaFields = toscaFields, ExampleValues = new HashSet<string>() });
                        fieldMaps[prop.Path].ExampleValues.Add(prop.Value.ToString());
                    }
                    foreach (var issueField in issueFields)
                    {
                        var field = fields.FirstOrDefault(x => x.id == issueField.Name);
                        string fieldName = (field != null ? field.name : issueField.Name);
                        if (issueField.Value is JValue)
                        {
                            if (!fieldMaps.ContainsKey(issueField.Path))
                            {
                                fieldMaps.Add(issueField.Path, new FieldMapViewModel
                                {
                                    DisplayedName = fieldName,
                                    ExampleValues = new HashSet<string>(),
                                    JiraJSONPath = issueField.Path,
                                    Map = false,
                                    ToscaFields = toscaFields
                                });
                            }
                            fieldMaps[issueField.Path].ExampleValues.Add(issueField.Value.ToString());
                        }
                        else
                        {
                            JObject fieldValue = issueField.Value as JObject;
                            if (fieldValue == null || fieldValue.Properties() == null)
                                continue;
                            var fieldValueProps = fieldValue.Properties().Where(x => x.Value is JValue);
                            foreach (var fieldValueProp in fieldValueProps)
                            {
                                if (!fieldMaps.ContainsKey(fieldValueProp.Path))
                                {
                                    fieldMaps.Add(fieldValueProp.Path, new FieldMapViewModel
                                    {
                                        DisplayedName = $"{fieldName} -> {fieldValueProp.Name}",
                                        ExampleValues = new HashSet<string>(),
                                        JiraJSONPath = fieldValueProp.Path,
                                        Map = false,
                                        ToscaFields = toscaFields
                                    });
                                }
                                fieldMaps[fieldValueProp.Path].ExampleValues.Add(fieldValueProp.Value.ToString());
                            }
                        }
                    }
                }
                foreach (var map in fieldMaps.OrderBy(x => x.Value.DisplayedName))
                {
                    if (Config.Config.parentLocatorPropertyOverride == map.Value.JiraJSONPath)
                        map.Value.ParentLocator = true;
                    FieldMaps.Add(map.Value);
                }
                foreach (var configMap in Config.Config.fieldMaps)
                {
                    if (FieldMaps.Any(x => x.JiraJSONPath == configMap.jiraJsonPath))
                    {
                        var matchingFM = FieldMaps.First(x => x.JiraJSONPath == configMap.jiraJsonPath);
                        matchingFM.Map = true;
                        matchingFM.ToscaField = configMap.toscaField;
                    }
                }
                errorMessage.Content = $"Success - {issues.Length} issues match";
                expanderGrid.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                errorMessage.Content = $"Error: {ex.Message}";
            }
        }

        private void Done_Click(object sender, RoutedEventArgs e)
        {
            Config.Config.fieldMaps = new List<JiraService.Configuration.FieldMap>();
            foreach (var fm in FieldMaps)
            {
                if (fm.ParentLocator)
                    Config.Config.parentLocatorPropertyOverride = fm.JiraJSONPath;
                if (fm.Map)
                {
                    Config.Config.fieldMaps.Add(new JiraService.Configuration.FieldMap
                    {
                        direction = JiraService.Configuration.Direction.jira_to_tosca,
                        jiraJsonPath = fm.JiraJSONPath,
                        toscaField = fm.ToscaField
                    });
                }
            }
            this.Close();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            //Application.Current.Shutdown();
        }
    }
}
