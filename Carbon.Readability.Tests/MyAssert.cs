﻿using System;
using NUnit.Framework;

namespace Carbon.Readability.Tests
{
  public static class MyAssert
  {
    public static void AssertSubstringCount(int expectedCount, string s, string substring)
    {
      Assert.IsNotNull(s);
      Assert.IsNotNull(substring);

      s = s.ToLower();
      substring = substring.ToLower();

      int index = -1;
      int count = 0;

      while ((index = s.IndexOf(substring, index + 1, StringComparison.Ordinal)) != -1)
      {
        count++;
      }

      Assert.AreEqual(expectedCount, count);
    }
  }
}
