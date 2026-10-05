using LibraHub.Infrastructure;
using Xunit;

namespace LibraHub.Tests;

/// <summary>Feature F6 business rule: "ISBN check digit validated (ISBN-10 and ISBN-13)".</summary>
public class IsbnTests
{
    [Theory]
    [InlineData("9780132350884")]  // Clean Code, ISBN-13
    [InlineData("0132350882")]     // Clean Code, ISBN-10
    [InlineData("978-1-4493-7332-0")]
    public void Valid_isbns_pass(string isbn) => Assert.True(Isbn.IsValid(isbn));

    [Theory]
    [InlineData("9780000000000")]
    [InlineData("123")]
    [InlineData("978000000000X")]
    public void Invalid_isbns_fail(string isbn) => Assert.False(Isbn.IsValid(isbn));

    [Fact]
    public void Isbn10_converts_to_correct_isbn13()
    {
        var isbn13 = Isbn.ToIsbn13("0132350882");
        Assert.Equal("9780132350884", isbn13);
        Assert.True(Isbn.IsValid(isbn13));
    }
}
