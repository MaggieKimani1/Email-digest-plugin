using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Graph.Models;
using Microsoft.Graph;
using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace sample_plugins.Plugins
{
    public sealed class TaskPlugin
    {
        private static readonly IConfigurationRoot _config = ConfigurationProvider.GetConfiguration();
        private static string _clientId = _config.GetSection("MSGraph:ClientId").Get<string>();
        private static string _tenantId = _config?.GetSection("MSGraph:TenantId").Get<string>();
        private static string _clientSecret = _config.GetSection("MSGraph:ClientSecret").Get<string>();

        [KernelFunction, Description("Create a new TODO task")]
        public static async Task<TodoTask> CreateTaskAsync(string report)
        {
            // Call Graph API /me/todo/lists/{task-ID}/tasks endpoint
            // Create a Graph client and use it to send the request
            var credential = new ClientSecretCredential(_tenantId, _clientId, _clientSecret);

            var graphClient = new GraphServiceClient(credential);

            var taskList = ExtractTodoListFromEmailReport(report);

            TodoTask newTasks = null;
            foreach (var task in taskList)
            {
                newTasks = await graphClient.Users["admin@M365x03432055.onmicrosoft.com"].Todo
                    .Lists["AQMkAGM4MmM3NDZjLTMyMDQtNDdjZS05YjEyLTNmOGUyNjM5NDY3NQAuAAAD1T5I5mDMwECzsjLq87xCQgABAPqL8Sha1R9Npa8l7fecTOsAAAIBEgAAAA=="].Tasks
                    .PostAsync(task);
            }

            return newTasks ?? throw new InvalidOperationException("Task creation failed.");
        }

        private static IList<TodoTask> ExtractTodoListFromEmailReport(string report)
        {
            var todoList = new List<TodoTask>();
            var todoString = report.Split("## Todo List:").Last();
            var todoItems = todoString.Split("- [ ]");
            foreach (var item in todoItems)
            {
                if (!string.IsNullOrWhiteSpace(item))
                {
                    var newTask = new TodoTask
                    {
                        Title = item,
                        Status = Microsoft.Graph.Models.TaskStatus.NotStarted
                    };

                    todoList.Add(newTask);
                }
            }

            return todoList;
        }
    }
}
