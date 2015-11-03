using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
//using Newtonsoft.Json;
using FluxCore.Adaptors;
using FluxCore.Authenticators;
using Core.Model;

namespace Core.Client {
  /// <summary>
  /// API client is mainly responible for making the HTTP call to the API backend
  /// </summary>
  public class ApiClient {

    public AuthenticatorInterface fluxAuthenticator;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiClient"/> class.
    /// </summary>
    /// <param name="basePath">The base path.</param>
    public ApiClient(String basePath="https://null/") {
      this.basePath = basePath;
      this.restClient = new RestClient(this.basePath);
    }

    /// <summary>
    /// Gets or sets the base path.
    /// </summary>
    /// <value>The base path.</value> 
    public string basePath { get; set; }

    /// <summary>
    /// Gets or sets the RestClient
    /// </summary>
    /// <value>The RestClient.</value> 
    public RestClient restClient { get; set; }

    private Dictionary<String, String> defaultHeaderMap = new Dictionary<String, String>();

    public object CallApi(String Path, RestSharp.Method Method, Dictionary<String, String> QueryParams, String PostBody,
      Dictionary<String, String> HeaderParams, Dictionary<String, String> FormParams, Dictionary<String, String> FileParams, String[] AuthSettings) {
      var response = new object();
      CallApiAsync(Path, Method, QueryParams, PostBody, HeaderParams, FormParams, FileParams, AuthSettings, res => {response = res;});
      while(response == null){}
        return response;
    }

    public virtual void CallApiAsync(String Path, RestSharp.Method Method, Dictionary<String, String> QueryParams, String PostBody,
      Dictionary<String, String> HeaderParams, Dictionary<String, String> FormParams, Dictionary<String, String> FileParams, String[] AuthSettings, Action<Response> callback) {

      var request = new RestRequest(Path, Method);

      UpdateParamsForAuth(QueryParams, HeaderParams, AuthSettings);

      if(fluxAuthenticator != null)
      request = fluxAuthenticator.authorize(request, this.basePath , Path, Method, QueryParams, PostBody, HeaderParams);

      // add default header, if any
      foreach(KeyValuePair<string, string> defaultHeader in this.defaultHeaderMap)
        request.AddHeader(defaultHeader.Key, defaultHeader.Value);

      // add header parameter, if any
      foreach(KeyValuePair<string, string> param in HeaderParams)
        request.AddHeader(param.Key, param.Value);
     
      // add query parameter, if any
      foreach(KeyValuePair<string, string> param in QueryParams)
        request.AddQueryParameter(param.Key, param.Value);

      // add form parameter, if any
      foreach(KeyValuePair<string, string> param in FormParams)
        request.AddParameter(param.Key, param.Value);

      // add file parameter, if any
      foreach(KeyValuePair<string, string> param in FileParams)
        request.AddFile(param.Key, param.Value);

      if (PostBody != null) {
        request.AddParameter("application/json", PostBody, ParameterType.RequestBody); // http body (model) parameter
      }

      restClient.ExecuteAsync(request, delegate(IRestResponse response){
      try{
        Response res = new Response();
        if (((int)response.StatusCode) >= 400) {
            throw new ApiException ((int)response.StatusCode, "Error calling GetApps: " + response.Content, response.Content);
        }
          res.code = (int)response.StatusCode;
          res.status = "success";
          res.data = response.Content;

        callback(res);
      }
      catch(ApiException e){
        Response res = new Response();
          res.code = (int)response.StatusCode;
          res.status = "error";
          res.data = response.Content;

        callback(res);
        //throw e;
      }
      catch(Exception er){
        throw new ApiException (500, "Error calling GetApps: " + er.Message);
      }

      }

      );

    }

    /// <summary>
    /// Add default header
    /// </summary>
    /// <param name="key">  Header field name
    /// <param name="value"> Header field value
    /// <returns></returns>
    public virtual void AddDefaultHeader(string key, string value) {
       defaultHeaderMap.Add(key, value);
    }

    /// <summary>
    /// Get default header
    /// </summary>
    /// <returns>Dictionary of default header</returns>
    public virtual Dictionary<String, String> GetDefaultHeader() {
       return defaultHeaderMap;
    }

    /// <summary>
    /// escape string (url-encoded)
    /// </summary>
    /// <param name="str"> String to be escaped
    /// <returns>Escaped string</returns>
    public virtual string EscapeString(string str) {
      return str;
    }

    /// <summary>
    /// if parameter is DateTime, output in ISO8601 format
    /// if parameter is a list of string, join the list with ","
    /// otherwise just return the string
    /// </summary>
    /// <param name="obj"> The parameter (header, path, query, form)
    /// <returns>Formatted string</returns>
    public virtual string ParameterToString(object obj)
    {
      if (obj is DateTime) {
        return ((DateTime)obj).ToString ("u");
      } else if (obj is List<string>) {
        return String.Join(",", (obj as List<string>).ToArray());
      } else {
  return Convert.ToString (obj);
      }
    }

    /// <summary>
    /// Deserialize the JSON string into a proper object
    /// </summary>
    /// <param name="json"> JSON string
    /// <param name="type"> Object type
    /// <returns>Object representation of the JSON string</returns>
    //public virtual object Deserialize(string content, Type type) {
    public virtual T Deserialize<T>(string content) {
      try
      {
          return Json.toObject<T>(content);
      }
      catch (IOException e) {
        throw new ApiException(500, e.Message);
      }
    }

    public virtual object Deserialize(string content) {
      try
      {
          return Json.toObject(content);
      }
      catch (IOException e) {
        throw new ApiException(500, e.Message);
      }
    }

    /// <summary>
    /// Serialize an object into JSON string
    /// </summary>
    /// <param name="obj"> Object 
    /// <returns>JSON string</returns>
    public virtual string Serialize(object obj) {
      try
      {
          return obj != null ? Json.toJson(obj) : null;
          //return obj != null ? JsonConvert.SerializeObject(obj) : null;
      }
      catch (Exception e) {
        throw new ApiException(500, e.Message);
      }
    }

    /// <summary>
    /// Get the API key with prefix
    /// </summary>
    /// <param name="obj"> Object 
    /// <returns>API key with prefix</returns>
    public virtual string GetApiKeyWithPrefix (string apiKey)
    {
      var apiKeyValue = "";
      Configuration.apiKey.TryGetValue (apiKey, out apiKeyValue);
      var apiKeyPrefix = "";
      if (Configuration.apiKeyPrefix.TryGetValue (apiKey, out apiKeyPrefix)) {
        return apiKeyPrefix + " " + apiKeyValue;
      } else {
        return apiKeyValue;
      }
    }

    /// <summary>
    /// Update parameters based on authentication
    /// </summary>
    /// <param name="QueryParams">Query parameters</param>
    /// <param name="HeaderParams">Header parameters</param>
    /// <param name="AuthSettings">Authentication settings</param>
    public virtual void UpdateParamsForAuth(Dictionary<String, String> QueryParams, Dictionary<String, String> HeaderParams, string[] AuthSettings) {
      if (AuthSettings == null || AuthSettings.Length == 0)
        return;
  
      foreach (string auth in AuthSettings) {
        // determine which one to use
        switch(auth) {
          
          default:
            //TODO show warning about security definition not found
          break;
        }
      }

    }

    /// <summary>
    /// Default Exception Handle 
    /// </summary>
    /// <param name="exc">ApiException</param>
    public virtual void DefaultExceptionHandler(ApiException exc) {
      Console.WriteLine (exc);
    }

    /// <summary>
    /// Encode string in base64 format 
    /// </summary>
    /// <param name="text">String to be encoded</param>
    public static string Base64Encode(string text) {
      var textByte = System.Text.Encoding.UTF8.GetBytes(text);
      return System.Convert.ToBase64String(textByte);
    }

  }
}
