//-----------------------------------------------------------------------
// Copyright (c) Microsoft Open Technologies, Inc.
// All Rights Reserved
// Apache License 2.0
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
// http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//-----------------------------------------------------------------------

using Microsoft.IdentityModel.Test;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IdentityModel.Tokens;

namespace System.IdentityModel.Test
{
    /// <summary>
    /// Test some key extensibility scenarios
    /// </summary>
    [TestClass]
    public class ExtensibilityTests
    {
        /// <summary>
        /// Test Context Wrapper instance on top of TestContext. Provides better accessor functions
        /// </summary>
        protected TestContextProvider _testContextProvider;

        public TestContext TestContext { get; set; }

        [ClassInitialize]
        public static void ClassSetup(TestContext testContext)
        { }

        [ClassCleanup]
        public static void ClassCleanup()
        { }

        [TestInitialize]
        public void Initialize()
        {
            _testContextProvider = new TestContextProvider(TestContext);
        }

        [TestMethod]
        [TestProperty("TestCaseID", "65A4AD1F-100F-41C3-AD84-4FE08C1F9A6D")]
        [Description("Extensibility tests for JwtSecurityTokenHandler")]
        public void JwtSecurityTokenHandler_Extensibility()
        {
            DerivedJwtSecurityTokenHandler handler = new DerivedJwtSecurityTokenHandler()
            {
                DerivedTokenType = typeof(DerivedJwtSecurityToken)
            };

            JwtSecurityToken jwt =
                new JwtSecurityToken
                (
                    issuer: Issuers.GotJwt,
                    audience: Audiences.AuthFactors,
                    claims: ClaimSets.Simple(Issuers.GotJwt, Issuers.GotJwt),
                    signingCredentials: KeyingMaterial.DefaultSymmetricSigningCreds_256_Sha2,
                    expires: DateTime.UtcNow + TimeSpan.FromHours(10),
                    notBefore: DateTime.UtcNow
                );

            string encodedJwt = handler.WriteToken(jwt);
            TokenValidationParameters tvp = new TokenValidationParameters()
            {
                IssuerSigningKey = KeyingMaterial.DefaultSymmetricSecurityKey_256,
                ValidateAudience = false,
                ValidIssuer = Issuers.GotJwt,
            };

            ValidateDerived(encodedJwt, handler, tvp, ExpectedException.NoExceptionExpected);
        }

        private void ValidateDerived(string jwt, DerivedJwtSecurityTokenHandler handler, TokenValidationParameters validationParameters, ExpectedException expectedException)
        {
            try
            {
                SecurityToken validatedToken;
                handler.ValidateToken(jwt, validationParameters, out validatedToken);
                Assert.IsNotNull(handler.Jwt as DerivedJwtSecurityToken);
                Assert.IsTrue(handler.ReadTokenCalled);
                Assert.IsTrue(handler.ValidateAudienceCalled);
                Assert.IsTrue(handler.ValidateIssuerCalled);
                Assert.IsTrue(handler.ValidateIssuerSigningKeyCalled);
                Assert.IsTrue(handler.ValidateLifetimeCalled);
                Assert.IsTrue(handler.ValidateSignatureCalled);
                expectedException.ProcessNoException();
            }
            catch (Exception ex)
            {
                expectedException.ProcessException(ex);
            }
        }

        [TestMethod]
        [TestProperty("TestCaseID", "65A4AD1F-100F-41C3-AD84-4FE08C1F9A6D")]
        [Description("Extensibility tests for NamedKeySecurityKeyIdentifierClause")]
        public void NamedKeySecurityKeyIdentifierClause_Extensibility()
        {
            string clauseName = "kid";
            string keyId = Issuers.GotJwt;

            NamedKeySecurityKeyIdentifierClause clause = new NamedKeySecurityKeyIdentifierClause(clauseName, keyId);
            SecurityKeyIdentifier keyIdentifier = new SecurityKeyIdentifier(clause);
            SigningCredentials signingCredentials = new SigningCredentials(KeyingMaterial.DefaultSymmetricSecurityKey_256, SecurityAlgorithms.HmacSha256Signature, SecurityAlgorithms.Sha256Digest, keyIdentifier);
            JwtHeader jwtHeader = new JwtHeader(signingCredentials);
            SecurityKeyIdentifier ski = jwtHeader.SigningKeyIdentifier;
            Assert.AreEqual(ski.Count, 1, "ski.Count != 1 ");

            NamedKeySecurityKeyIdentifierClause clauseOut = ski.Find<NamedKeySecurityKeyIdentifierClause>();
            Assert.IsNotNull(clauseOut, "NamedKeySecurityKeyIdentifierClause not found");
            Assert.AreEqual(clauseOut.Name, clauseName, "clauseOut.Id != clauseId");
            Assert.AreEqual(clauseOut.Id, keyId, "clauseOut.KeyIdentifier != keyId");

            NamedKeySecurityToken NamedKeySecurityToken = new NamedKeySecurityToken(clauseName, keyId, new SecurityKey[] { KeyingMaterial.DefaultSymmetricSecurityKey_256 });
            Assert.IsTrue(NamedKeySecurityToken.MatchesKeyIdentifierClause(clause), "NamedKeySecurityToken.MatchesKeyIdentifierClause( clause ), failed");

            List<SecurityKey> list = new List<SecurityKey>() { KeyingMaterial.DefaultSymmetricSecurityKey_256 };
            Dictionary<string, IList<SecurityKey>> keys = new Dictionary<string, IList<SecurityKey>>() { { "kid", list }, };
            NamedKeyIssuerTokenResolver nkitr = new NamedKeyIssuerTokenResolver(keys: keys);
            SecurityKey sk = nkitr.ResolveSecurityKey(clause);
            Assert.IsNotNull(sk, "NamedKeySecurityToken.MatchesKeyIdentifierClause( clause ), failed");
        }

        [TestMethod]
        [TestProperty("TestCaseID", "C4FC2FC1-5AB0-4A73-A620-59D1FBF92D7A")]
        [Description("Algorithm names can be mapped inbound and outbound (AsymmetricSignatureProvider)")]
        public void AsymmetricSignatureProvider_Extensibility()
        {
            JwtSecurityTokenHandler handler = new JwtSecurityTokenHandler();
            string newAlgorithmValue = "bobsYourUncle";

            string originalAlgorithmValue = ReplaceAlgorithm(SecurityAlgorithms.RsaSha256Signature, newAlgorithmValue, JwtSecurityTokenHandler.OutboundAlgorithmMap);
            JwtSecurityToken jwt = handler.CreateToken(issuer: IdentityUtilities.DefaultIssuer, audience: IdentityUtilities.DefaultAudience, signingCredentials: KeyingMaterial.DefaultX509SigningCreds_2048_RsaSha2_Sha2) as JwtSecurityToken;
            ReplaceAlgorithm(SecurityAlgorithms.RsaSha256Signature, originalAlgorithmValue, JwtSecurityTokenHandler.OutboundAlgorithmMap);

            // outbound mapped algorithm is "bobsYourUncle", inbound map will not find this
            ExpectedException expectedException = ExpectedException.SignatureVerificationFailedException(substringExpected: "IDX10502:", innerTypeExpected: typeof(InvalidOperationException));
            RunAlgorithmMappingTest(jwt.RawData, IdentityUtilities.DefaultAsymmetricTokenValidationParameters, handler, expectedException);

            // inbound is mapped to Rsa256
            originalAlgorithmValue = ReplaceAlgorithm(newAlgorithmValue, SecurityAlgorithms.RsaSha256Signature, JwtSecurityTokenHandler.InboundAlgorithmMap);
            RunAlgorithmMappingTest(jwt.RawData, IdentityUtilities.DefaultAsymmetricTokenValidationParameters, handler, ExpectedException.NoExceptionExpected);
            ReplaceAlgorithm(newAlgorithmValue, originalAlgorithmValue, JwtSecurityTokenHandler.InboundAlgorithmMap);
        }

        [TestMethod]
        [TestProperty("TestCaseID", "A8068888-87D8-49D6-919F-CDF9AAC26F57")]
        [Description("Algorithm names can be mapped inbound and outbound (SymmetricSignatureProvider)")]
        public void SymmetricSignatureProvider_Extensibility()
        {
            JwtSecurityTokenHandler handler = new JwtSecurityTokenHandler();
            string newAlgorithmValue = "bobsYourUncle";

            string originalAlgorithmValue = ReplaceAlgorithm(SecurityAlgorithms.HmacSha256Signature, newAlgorithmValue, JwtSecurityTokenHandler.OutboundAlgorithmMap);
            JwtSecurityToken jwt = handler.CreateToken(issuer: IdentityUtilities.DefaultIssuer, audience: IdentityUtilities.DefaultAudience, signingCredentials: KeyingMaterial.DefaultSymmetricSigningCreds_256_Sha2) as JwtSecurityToken;
            ReplaceAlgorithm(SecurityAlgorithms.HmacSha256Signature, originalAlgorithmValue, JwtSecurityTokenHandler.OutboundAlgorithmMap);

            // outbound mapped algorithm is "bobsYourUncle", inbound map will not find this
            ExpectedException expectedException = ExpectedException.SignatureVerificationFailedException(innerTypeExpected: typeof(InvalidOperationException), substringExpected: "IDX10503:");
            RunAlgorithmMappingTest(jwt.RawData, IdentityUtilities.DefaultSymmetricTokenValidationParameters, handler, expectedException);

            // inbound is mapped Hmac
            originalAlgorithmValue = ReplaceAlgorithm(newAlgorithmValue, SecurityAlgorithms.HmacSha256Signature, JwtSecurityTokenHandler.InboundAlgorithmMap);
            RunAlgorithmMappingTest(jwt.RawData, IdentityUtilities.DefaultSymmetricTokenValidationParameters, handler, ExpectedException.NoExceptionExpected);
            ReplaceAlgorithm(newAlgorithmValue, originalAlgorithmValue, JwtSecurityTokenHandler.InboundAlgorithmMap);
        }

        private void RunAlgorithmMappingTest(string jwt, TokenValidationParameters validationParameters, JwtSecurityTokenHandler handler, ExpectedException expectedException)
        {
            try
            {
                SecurityToken validatedToken;
                handler.ValidateToken(jwt, validationParameters, out validatedToken);
                expectedException.ProcessNoException();
            }
            catch (Exception ex)
            {
                expectedException.ProcessException(ex);
            }
        }

        private string ReplaceAlgorithm(string algorithmKey, string newAlgorithmValue, IDictionary<string, string> algorithmMap)
        {
            string originalAlgorithmValue = null;
            if (algorithmMap.TryGetValue(algorithmKey, out originalAlgorithmValue))
            {
                algorithmMap.Remove(algorithmKey);
            }

            if (!string.IsNullOrWhiteSpace(newAlgorithmValue))
            {
                algorithmMap.Add(algorithmKey, newAlgorithmValue);
            }

            return originalAlgorithmValue;
        }
    }
}
