module Djehuti.Api.OAuth

open System
open System.Net.Http
open System.Text.Json

type GoogleTokenResponse = {
  access_token: string
  token_type: string
  expires_in: int
  id_token: string
}

type GoogleUserInfo = {
  id: string
  email: string
  name: string option
  picture: string option
}

type GitHubTokenResponse = {
  access_token: string
  token_type: string
  scope: string
}

type GitHubUserInfo = {
  id: int
  login: string
  email: string option
  name: string option
  avatar_url: string option
}

let private parseJson<'T> (json: string) : 'T option =
  try
    let options = JsonSerializerOptions(PropertyNameCaseInsensitive = true)
    Some (JsonSerializer.Deserialize<'T>(json, options))
  with _ ->
    None

let exchangeGoogleCode (code: string) (clientId: string) (clientSecret: string) : Async<GoogleTokenResponse option> =
  async {
    try
      use client = new HttpClient()
      let content = new FormUrlEncodedContent([
        new System.Collections.Generic.KeyValuePair<string, string>("code", code)
        new System.Collections.Generic.KeyValuePair<string, string>("client_id", clientId)
        new System.Collections.Generic.KeyValuePair<string, string>("client_secret", clientSecret)
        new System.Collections.Generic.KeyValuePair<string, string>("redirect_uri", "https://djehuti.lagdaemon.com/api/auth/oauth/google/callback")
        new System.Collections.Generic.KeyValuePair<string, string>("grant_type", "authorization_code")
      ])

      let! response = client.PostAsync("https://oauth2.googleapis.com/token", content) |> Async.AwaitTask
      if response.IsSuccessStatusCode then
        let! body = response.Content.ReadAsStringAsync() |> Async.AwaitTask
        return parseJson<GoogleTokenResponse> body
      else
        return None
    with _ ->
      return None
  }

let getGoogleUserInfo (accessToken: string) : Async<GoogleUserInfo option> =
  async {
    try
      use client = new HttpClient()
      client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}")
      let! response = client.GetAsync("https://www.googleapis.com/oauth2/v2/userinfo") |> Async.AwaitTask
      if response.IsSuccessStatusCode then
        let! body = response.Content.ReadAsStringAsync() |> Async.AwaitTask
        return parseJson<GoogleUserInfo> body
      else
        return None
    with _ ->
      return None
  }

let exchangeGitHubCode (code: string) (clientId: string) (clientSecret: string) : Async<GitHubTokenResponse option> =
  async {
    try
      use client = new HttpClient()
      let content = new FormUrlEncodedContent([
        new System.Collections.Generic.KeyValuePair<string, string>("code", code)
        new System.Collections.Generic.KeyValuePair<string, string>("client_id", clientId)
        new System.Collections.Generic.KeyValuePair<string, string>("client_secret", clientSecret)
        new System.Collections.Generic.KeyValuePair<string, string>("redirect_uri", "https://djehuti.lagdaemon.com/api/auth/oauth/github/callback")
      ])

      let! response = client.PostAsync("https://github.com/login/oauth/access_token", content) |> Async.AwaitTask
      if response.IsSuccessStatusCode then
        let! body = response.Content.ReadAsStringAsync() |> Async.AwaitTask
        return parseJson<GitHubTokenResponse> body
      else
        return None
    with _ ->
      return None
  }

let getGitHubUserInfo (accessToken: string) : Async<GitHubUserInfo option> =
  async {
    try
      use client = new HttpClient()
      client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}")
      client.DefaultRequestHeaders.Add("User-Agent", "Djehuti")
      let! response = client.GetAsync("https://api.github.com/user") |> Async.AwaitTask
      if response.IsSuccessStatusCode then
        let! body = response.Content.ReadAsStringAsync() |> Async.AwaitTask
        return parseJson<GitHubUserInfo> body
      else
        return None
    with _ ->
      return None
  }
