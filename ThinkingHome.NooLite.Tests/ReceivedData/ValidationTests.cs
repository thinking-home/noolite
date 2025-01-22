using System;
using Xunit;

namespace ThinkingHome.NooLite.Tests.ReceivedData;

public class ValidationTests
{
    public const int VALID_DATA_SIZE = 17;

    [Fact]
    public void Constructor_ForNull_ThrowsException()
    {
        byte[] nullReceivedData = null;

        var ex = Assert.ThrowsAny<Exception>(() => new NooLite.ReceivedData(nullReceivedData));

        Assert.Contains("null", ex.Message);
    }

    [Fact]
    public void Constructor_ForInvalidDataSize_ThrowsException()
    {
        var invalidSizeData = new byte[3];

        var ex = Assert.ThrowsAny<Exception>(() => new NooLite.ReceivedData(invalidSizeData));

        Assert.Contains("length", ex.Message);
    }


    [Fact]
    public void Constructor_ForInvalidFirstByte_ThrowsException()
    {
        var testData = new byte[VALID_DATA_SIZE];
        testData[0] = 124;

        var ex = Assert.ThrowsAny<Exception>(() => new NooLite.ReceivedData(testData));

        Assert.Contains("start", ex.Message);
    }

    [Fact]
    public void Constructor_ForInvalidLastByte_ThrowsException()
    {
        var testData = new byte[VALID_DATA_SIZE];
        testData[0] = 173; // valid start marker
        testData[VALID_DATA_SIZE - 1] = 124;

        var ex = Assert.ThrowsAny<Exception>(() => new NooLite.ReceivedData(testData));

        Assert.Contains("stop", ex.Message);
    }
}
