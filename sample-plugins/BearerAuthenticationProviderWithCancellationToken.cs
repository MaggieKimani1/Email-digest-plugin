using System.Net.Http.Headers;

namespace sample_plugins
{
    /// <summary>
    /// Retrieves a token via the provided delegate and applies it to HTTP requests using the
    /// "bearer" authentication scheme.
    /// </summary>
    public class BearerAuthenticationProviderWithCancellationToken
    {
        private readonly Func<Task<string>> _bearerToken;

        /// <summary>
        /// Creates an instance of the <see cref="BearerAuthenticationProviderWithCancellationToken"/> class.
        /// </summary>
        /// <param name="bearerToken">Delegate to retrieve the bearer token.</param>
        public BearerAuthenticationProviderWithCancellationToken(Func<Task<string>> bearerToken)
        {
            this._bearerToken = bearerToken;
        }

        /// <summary>
        /// Applies the token to the provided HTTP request message.
        /// </summary>
        /// <param name="request">The HTTP request message.</param>
        /// <param name="cancellationToken"></param>
        public async Task AuthenticateRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
        {
            var token = await this._bearerToken().ConfigureAwait(false);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }
}
