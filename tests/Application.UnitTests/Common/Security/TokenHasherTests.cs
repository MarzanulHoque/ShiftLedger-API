using FluentAssertions;
using ShiftLedger.Application.Common.Security;
using Xunit;

namespace ShiftLedger.Application.UnitTests.Common.Security;

public class TokenHasherTests
{
    [Fact]
    public void Hash_IsDeterministic_ForSameToken()
    {
        var raw = TokenHasher.NewRawToken();
        TokenHasher.Hash(raw).Should().Be(TokenHasher.Hash(raw));
    }

    [Fact]
    public void Hash_DiffersForDifferentTokens_AndIsNotTheRawToken()
    {
        var a = TokenHasher.NewRawToken();
        var b = TokenHasher.NewRawToken();

        a.Should().NotBe(b);
        TokenHasher.Hash(a).Should().NotBe(TokenHasher.Hash(b));
        TokenHasher.Hash(a).Should().NotBe(a);
    }
}
