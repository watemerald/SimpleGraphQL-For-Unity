using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace SimpleGraphQL
{
    /// <summary>
    /// This API object is meant to be reused, so keep an instance of it somewhere!
    /// Multiple GraphQLClients can be used with different configs based on needs.
    /// </summary>
    [PublicAPI]
    public class GraphQLClient
    {
        public readonly List<Query> SearchableQueries;
        public readonly Dictionary<string, string> CustomHeaders;

        public string Endpoint;
        public string AuthScheme;

        public GraphQLClient(
            string endpoint,
            IEnumerable<Query> queries = null,
            Dictionary<string, string> headers = null,
            string authScheme = null
        )
        {
            Endpoint = endpoint;
            AuthScheme = authScheme;
            SearchableQueries = queries?.ToList();
            CustomHeaders = headers;
        }

        public GraphQLClient(GraphQLConfig config)
        {
            Endpoint = config.Endpoint;
            SearchableQueries = config.Files.SelectMany(x => x.Queries).ToList();
            CustomHeaders = config.CustomHeaders.ToDictionary(header => header.Key, header => header.Value);
            AuthScheme = config.AuthScheme;
        }

        /// <summary>
        /// Send a query!
        /// </summary>
        /// <param name="request">The request you are sending.</param>
        /// <param name="headers">Any headers you want to pass</param>
        /// <param name="authToken">The authToken</param>
        /// <param name="authScheme">The authScheme to be used.</param>
        /// <returns></returns>
        public async Task<string> Send(
            Request request,
            Dictionary<string, string> headers = null,
            string authToken = null,
            string authScheme = null
        )
        {
            if (CustomHeaders != null)
            {
                if (headers == null) headers = new Dictionary<string, string>();

                foreach (KeyValuePair<string, string> header in CustomHeaders)
                {
                    headers.Add(header.Key, header.Value);
                }
            }

            if (authScheme == null)
            {
                authScheme = AuthScheme;
            }

            string postQueryAsync = await HttpUtils.PostRequest(
                Endpoint,
                request,
                headers,
                authToken,
                authScheme
            );

            return postQueryAsync;
        }

        public async Task<Response<TResponse>> Send<TResponse>(
            Request request,
            Dictionary<string, string> headers = null,
            string authToken = null,
            string authScheme = null
        )
        {
            string json = await Send(request, headers, authToken, authScheme);
            return JsonConvert.DeserializeObject<Response<TResponse>>(json);
        }

        public async Task<Response<TResponse>> Send<TResponse>(
            Func<TResponse> responseTypeResolver,
            Request request,
            Dictionary<string, string> headers = null,
            string authToken = null,
            string authScheme = null)
        {
            return await Send<TResponse>(request, headers, authToken, authScheme);
        }

        /// <summary>
        /// Registers a listener for subscriptions.
        /// </summary>
        /// <param name="listener"></param>
        public void RegisterListener(Action<string> listener)
        {
            HttpUtils.SubscriptionDataReceived += listener;
        }

        /// <summary>
        /// Unregisters a listener for subscriptions.
        /// </summary>
        /// <param name="listener"></param>
        public void UnregisterListener(Action<string> listener)
        {
            HttpUtils.SubscriptionDataReceived -= listener;
        }

        /// <summary>
        /// Subscribe to a query in GraphQL.
        /// </summary>
        /// <param name="request">The request you are sending.</param>
        /// <param name="headers"></param>
        /// <param name="authToken"></param>
        /// <param name="authScheme"></param>
        /// <param name="protocol"></param>
        /// <returns>True if successful</returns>
        public async Task<bool> Subscribe(
            Request request,
            Dictionary<string, string> headers = null,
            string authToken = null,
            string authScheme = null,
            string protocol = "graphql-transport-ws"
        )
        {
            if (CustomHeaders != null)
            {
                if (headers == null) headers = new Dictionary<string, string>();

                foreach (KeyValuePair<string, string> header in CustomHeaders)
                {
                    headers.Add(header.Key, header.Value);
                }
            }

            if (authScheme == null)
            {
                authScheme = AuthScheme;
            }

            if (!HttpUtils.IsWebSocketReady())
            {
                // Prepare the socket before continuing.
                await HttpUtils.WebSocketConnect(Endpoint, headers, authToken, authScheme, protocol);
            }

            return await HttpUtils.WebSocketSubscribe(request.Query.ToMurmur2Hash().ToString(), request);
        }

        /// <summary>
        /// Subscribe to a query in GraphQL.
        /// </summary>
        /// <param name="id">A custom id to pass.</param>
        /// <param name="request"></param>
        /// <param name="headers"></param>
        /// <param name="authToken"></param>
        /// <param name="authScheme"></param>
        /// <param name="protocol"></param>
        /// <returns>True if successful</returns>
        public async Task<bool> Subscribe(
            string id,
            Request request,
            Dictionary<string, string> headers = null,
            string authToken = null,
            string authScheme = null,
            string protocol = "graphql-ws"
        )
        {
            if (CustomHeaders != null)
            {
                if (headers == null) headers = new Dictionary<string, string>();

                foreach (KeyValuePair<string, string> header in CustomHeaders)
                {
                    headers.Add(header.Key, header.Value);
                }
            }

            if (authScheme == null)
            {
                authScheme = AuthScheme;
            }

            if (!HttpUtils.IsWebSocketReady())
            {
                // Prepare the socket before continuing.
                await HttpUtils.WebSocketConnect(Endpoint, headers, authToken, authScheme, protocol);
            }

            return await HttpUtils.WebSocketSubscribe(id, request);
        }


        /// <summary>
        /// Unsubscribe from a request.
        /// </summary>
        /// <param name="request"></param>
        public async Task Unsubscribe(Request request)
        {
            if (!HttpUtils.IsWebSocketReady())
            {
                // Socket is already apparently closed, so this wouldn't work anyways.
                return;
            }

            await HttpUtils.WebSocketUnsubscribe(request.Query.ToMurmur2Hash().ToString());
        }

        /// <summary>
        /// Unsubscribe from an id.
        /// </summary>
        /// <param name="id"></param>
        public async Task Unsubscribe(string id)
        {
            if (!HttpUtils.IsWebSocketReady())
            {
                // Socket is already apparently closed, so this wouldn't work anyways.
                return;
            }

            await HttpUtils.WebSocketUnsubscribe(id);
        }

        /// <summary>
        /// Finds the first query located in a file.
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public Query FindQuery(string fileName)
        {
            return SearchableQueries?.FirstOrDefault(x => x.FileName == fileName);
        }

        /// <summary>
        /// Finds the first query located in a file.
        /// </summary>
        /// <param name="operationName"></param>
        /// <returns></returns>
        public Query FindQueryByOperation(string operationName)
        {
            return SearchableQueries?.FirstOrDefault(x => x.OperationName == operationName);
        }

        /// <summary>
        /// Finds a query by fileName and operationName.
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="operationName"></param>
        /// <returns></returns>
        public Query FindQuery(string fileName, string operationName)
        {
            return SearchableQueries?.FirstOrDefault(x => x.FileName == fileName && x.OperationName == operationName);
        }

        /// <summary>
        /// Finds a query by operationName and operationType.
        /// </summary>
        /// <param name="operationName"></param>
        /// <param name="operationType"></param>
        /// <returns></returns>
        public Query FindQuery(string operationName, OperationType operationType)
        {
            return SearchableQueries?.FirstOrDefault(x =>
                x.OperationName == operationName &&
                x.OperationType == operationType
            );
        }

        /// <summary>
        /// Finds a query by fileName, operationName, and operationType.
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="operationName"></param>
        /// <param name="operationType"></param>
        /// <returns></returns>
        public Query FindQuery(string fileName, string operationName, OperationType operationType)
        {
            return SearchableQueries?.FirstOrDefault(
                x => x.FileName == fileName &&
                     x.OperationName == operationName &&
                     x.OperationType == operationType
            );
        }

        /// <summary>
        /// Finds all queries with the given operation name.
        /// You may need to do additional filtering to get the query you want since they will all have the same operation name.
        /// </summary>
        /// <param name="operation"></param>
        /// <returns></returns>
        public List<Query> FindQueriesByOperation(string operation)
        {
            return SearchableQueries?.FindAll(x => x.OperationName == operation);
        }
    }
}