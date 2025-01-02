global using Xunit;
global using FluentAssertions;
global using MyBigInteger = Kzrnm.Numerics.BigInteger;
global using OrigBigInteger = System.Numerics.BigInteger;
#if NET8_0_OR_GREATER
global using PortBigInteger = Kzrnm.Numerics.Port.BigInteger;
#endif