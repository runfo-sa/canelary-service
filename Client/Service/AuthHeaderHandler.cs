using Client.Options;
using Core;
using Microsoft.Extensions.Options;

namespace Client.Service
{
    /// <summary>
    /// Inyecta los headers <c>request-key</c> y <c>request-hash</c> en cada request saliente
    /// hacia el Server.
    /// </summary>
    public sealed class AuthHeaderHandler(IOptions<AuthOptions> auth) : DelegatingHandler
    {
        private readonly AuthOptions _auth = auth.Value;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.TryAddWithoutValidation("request-key", _auth.ClavePublica);
            request.Headers.TryAddWithoutValidation("request-hash", Encryption.EncryptKey(_auth.ClavePublica, _auth.ClavePrivada));
            return base.SendAsync(request, cancellationToken);
        }
    }
}
