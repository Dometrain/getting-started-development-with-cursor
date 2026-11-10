using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace LivePoll.Simulator;

internal static class VirtualVoter
{
    public static async Task CastAsync(
        HttpClient client,
        Guid pollId,
        PlannedVote vote,
        CancellationToken cancellationToken)
    {
        try
        {
            using var signIn = await client.PostAsJsonAsync(
                "/auth/dev-token",
                new SignInRequest(vote.Username),
                JsonSerializerOptions.Web,
                cancellationToken);

            if (!signIn.IsSuccessStatusCode)
            {
                return;
            }

            var signedIn = await signIn.Content.ReadFromJsonAsync<SignInResponse>(
                JsonSerializerOptions.Web,
                cancellationToken);
            if (signedIn is null || string.IsNullOrEmpty(signedIn.Token))
            {
                return;
            }

            using var voteRequest = new HttpRequestMessage(HttpMethod.Post, $"/polls/{pollId}/votes")
            {
                Content = JsonContent.Create(
                    new CastVoteBody(vote.OptionId),
                    options: JsonSerializerOptions.Web),
            };
            voteRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", signedIn.Token);

            using var voteResponse = await client.SendAsync(voteRequest, cancellationToken);
            if (voteResponse.Content is not null)
            {
                await voteResponse.Content.ReadAsStringAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private sealed record SignInRequest(string Username);

    private sealed record SignInResponse(string Token);

    private sealed record CastVoteBody(Guid OptionId);
}