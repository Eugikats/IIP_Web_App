using InsuranceIntelligence.Api.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace InsuranceIntelligence.Api.Tests;

public sealed class TokenIssuerTests
{
    private static HttpContext WithToken(string issuer)
    {
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor { Issuer = issuer, Audience = "api", Expires = DateTime.UtcNow.AddMinutes(5) });
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = $"Bearer {token}";
        return context;
    }

    [Fact]
    public void A_token_from_an_External_ID_directory_goes_to_the_External_ID_check() =>
        Assert.True(TokenIssuer.IsExternalId(WithToken("https://11111111-1111-1111-1111-111111111111.ciamlogin.com/11111111-1111-1111-1111-111111111111/v2.0")));

    [Fact]
    public void A_work_account_token_stays_with_the_work_account_check() =>
        Assert.False(TokenIssuer.IsExternalId(WithToken("https://login.microsoftonline.com/dcb0b132-fb9e-475c-a68e-b0671222e8d8/v2.0")));

    [Theory]
    [InlineData("")]
    [InlineData("Bearer not-a-token")]
    [InlineData("Basic abc")]
    public void No_readable_token_goes_to_the_default_check(string header)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = header;

        Assert.False(TokenIssuer.IsExternalId(context));
    }

    [Theory]
    [InlineData("contoso.ciamlogin.com", true)]
    [InlineData("ciamlogin.com", true)]
    [InlineData("ciamlogin.com.evil.example", false)]
    [InlineData("notciamlogin.com", false)]
    public void Only_real_ciamlogin_hosts_count(string host, bool expected) =>
        Assert.Equal(expected, TokenIssuer.IsExternalIdHost(host));
}
