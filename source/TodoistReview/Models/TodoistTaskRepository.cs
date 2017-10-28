﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RestSharp;
using TodoistReview.Models.TodoistApiModels;

namespace TodoistReview.Models
{
    public class TodoistTaskRepository : ITaskRepository
    {
        private const String ApiUrl = "https://todoist.com/API/v6/";
        private readonly String _authToken;

        public TodoistTaskRepository(String authToken)
        {
            _authToken = authToken;
        }

        public IList<Label> GetAllLabels()
        {
            var client = new RestClient(ApiUrl);

            var request = new RestRequest("sync", Method.POST);
            request.AddParameter("token", _authToken);
            request.AddParameter("seq_no", "0");
            request.AddParameter("resource_types", "[\"labels\"]");

            IRestResponse<TodoistLabelsResponse> response = client.Execute<TodoistLabelsResponse>(request);

            return response.Data.Labels;
        }

        public IList<TodoTask> GetAllTasks()
        {
            var client = new RestClient(ApiUrl);

            var request = new RestRequest("sync", Method.POST);
            request.AddParameter("token", _authToken);

            // Sequence number, used to allow client to perform incremental sync. Pass 0 to retrieve all active resource data. 
            request.AddParameter("seq_no", "0");
            request.AddParameter("resource_types", "[\"items\"]");

            IRestResponse<TodoistTasksResponse> response = client.Execute<TodoistTasksResponse>(request);

            return response.Data.Items;
        }

        public String UpdateTasks(List<TodoTask> tasksToUpdate)
        {
            if (tasksToUpdate.Count == 0)
            {
                return "Empty list of tasks";
            }
            if (tasksToUpdate.Any(task => task.labels == null))
            {
                return "List of tasks contains at least one invalid item";
            }

            var client = new RestClient(ApiUrl);

            var request = new RestRequest("sync", Method.POST);
            request.AddParameter("token", _authToken);

            // build json command as string (a shortcut)
            var commandsString = new StringBuilder();
            commandsString.Append("[");
            for (var i = 0; i < tasksToUpdate.Count; i++)
            {
                String commandString = GetUpdateCommandString(tasksToUpdate[i]);
                commandsString.Append(commandString);

                if (i != tasksToUpdate.Count - 1)
                {
                    commandsString.Append(",");
                }
            }

            commandsString.Append("]");


            request.AddParameter("commands", commandsString.ToString());

            IRestResponse<TodoistTasksResponse> response = client.Execute<TodoistTasksResponse>(request);
            String apiResponse = response.Content;
            return apiResponse;
        }

        private String GetUpdateCommandString(TodoTask task)
        {
            String commandString;
            Guid commandId = Guid.NewGuid();

            if (task.IsToBeDeleted)
            {
                // as in documentation, https://developer.todoist.com/sync/v7/#delete-items
                commandString =
                    $"{{\"type\": \"item_delete\", \"uuid\": \"{commandId}\", \"args\": {{\"ids\": [{task.id}] }}}}";
            }
            else
            {
                // typical use case: update labels
                List<Int64> specialLabelsIds = Label.SpecialLabels.Select(x => x.id).ToList();
                IEnumerable<Int64> labelsExcludingSpecial = task.labels.Where(x => !specialLabelsIds.Contains(x));
                String labelsArrayString =
                    "[" + String.Join(",", labelsExcludingSpecial) + "]"; // JSON array with int64 ids

                commandString =
                    $"{{\"type\": \"item_update\", \"uuid\": \"{commandId}\", \"args\": {{\"id\": {task.id}, \"priority\": {task.priority}, \"labels\": {labelsArrayString}}}}}";
            }
            
            return commandString;
        }
    }
}