# Changelog

## [3.0.1](https://github.com/atc-net/atc-rest-minimalapi/compare/v3.0.0...v3.0.1) (2026-10-15)


### Bug fixes

* **sample:** send only filled-in fields from the Blazor user forms ([b9388b3](https://github.com/atc-net/atc-rest-minimalapi/commit/b9388b3b2665553ccedf55d362ae09f5dcebfdd9))

## [3.0.0](https://github.com/atc-net/atc-rest-minimalapi/compare/v2.0.41...v3.0.0) (2026-10-12)


### ⚠ BREAKING CHANGES

* **validation:** a body nested deeper than the validation depth now gets a 400 instead of passing unvalidated.
* **validation:** a request whose object fails a property rule no longer also gets that object's (or a parent's) IValidatableObject.Validate or type-level attribute errors in the same 400; they appear once the property errors are fixed.
* **validation:** a 400 for equal invalid records now lists every one of them; the package no longer depends on MiniValidation, so code that used its types through this package must reference it directly ([SkipRecursion] from that package is still honoured).
* **middleware:** a 400 from a ValidationException now has the title "One or more validation errors occurred." and an errors object instead of the message in detail; a DataAnnotations message without a member name moves from detail to errors[""].
* **validation:** validation errors of a body bound by BindAsync (no [FromBody], e.g. a body offered as XML) are keyed relative to the body instead of under the member name, and XML requests are keyed by their XML names.
* **validation:** errors inside an array body are keyed [n].field instead of field.
* **validation:** clients that read errors["Request"] for a missing body must read errors[""]; the message no longer names the C# member.
* **middleware:** error responses no longer carry headers set before the exception (only OnStarting headers survive) and are never cached; BadHttpRequestException returns its own status code; instance includes the path base.
* **validation:** the keys of the `errors` object changed from C# member paths (e.g. `Request.Reason`, `PageSize`) to wire names (e.g. `reason`, `page-size`). Set `KeyStyle = ValidationKeyStyle.MemberPath` to keep the old keys.
* **middleware:** write errors through IProblemDetailsService
* **middleware:** stop leaking exception details by default
* **middleware:** map server-side exceptions to 500 by default
* **middleware:** align error bodies with ASP.NET Core ProblemDetails

### New features

* **aot:** mark assembly scanning and the Swagger filters as not trim-safe ([5c34baf](https://github.com/atc-net/atc-rest-minimalapi/commit/5c34baf0458addc4c8a1daacebdeff75e298d6cb))
* **middleware:** align error bodies with ASP.NET Core ProblemDetails ([f86410c](https://github.com/atc-net/atc-rest-minimalapi/commit/f86410c45e69066eb115c6e36a5f8fe7c662d408))
* **middleware:** keep the field errors of a ValidationException ([b03fcf4](https://github.com/atc-net/atc-rest-minimalapi/commit/b03fcf4a305b059d9ca7f8dc2b2edea84f4a3ff6))
* **middleware:** log exceptions handled by GlobalErrorHandlingMiddleware ([2514f48](https://github.com/atc-net/atc-rest-minimalapi/commit/2514f48f7e5aa8132f131dcc33add7726eaa2c52))
* **middleware:** map server-side exceptions to 500 by default ([c6bca8b](https://github.com/atc-net/atc-rest-minimalapi/commit/c6bca8b1c74f2ee0194cdb04c747f0973ce94ac2))
* **middleware:** reset responses and align errors with ASP.NET Core ([0e4e958](https://github.com/atc-net/atc-rest-minimalapi/commit/0e4e958d08eb5d2b631ad2e5e88be9bcd80acd2e))
* **middleware:** stop leaking exception details by default ([dc0b47a](https://github.com/atc-net/atc-rest-minimalapi/commit/dc0b47a4e7af21509a22997328d4f4dc1b46d8d9))
* **middleware:** write errors through IProblemDetailsService ([102d40f](https://github.com/atc-net/atc-rest-minimalapi/commit/102d40f266b4b4919974e534c785415d17bddb9c))
* **sample:** add error-handling simulation endpoints ([7fd8a3a](https://github.com/atc-net/atc-rest-minimalapi/commit/7fd8a3a99d594af82f07e958780817315b78cbbc))
* **sample:** add React sample with Material UI CRUD ([4c84a4a](https://github.com/atc-net/atc-rest-minimalapi/commit/4c84a4af6e4bc9d1d5d16f1d5bd94fc03c793251))
* **sample:** bind Blazor form errors by wire-name keys ([44e77e5](https://github.com/atc-net/atc-rest-minimalapi/commit/44e77e5c20ade7a3fcff09bee9e8e694a7f7a6f6))
* **sample:** return ProblemDetails for all API errors ([442b707](https://github.com/atc-net/atc-rest-minimalapi/commit/442b7075a80e7f10628eb1859225f603b17e6b92))
* **validation:** keep the row index of an array body ([e9b79ff](https://github.com/atc-net/atc-rest-minimalapi/commit/e9b79fffcfffcdb10dad53412a6693f754a7c9e6))
* **validation:** report a missing body under the empty key ([164e4fd](https://github.com/atc-net/atc-rest-minimalapi/commit/164e4fd3fdade2370b4249eb4155dd2b8f655dae))
* **validation:** skip Validate and type-level attributes once a member failed ([0cf51c7](https://github.com/atc-net/atc-rest-minimalapi/commit/0cf51c73a06f9a8a5befb7d98704268bceff4a7e))
* **validation:** treat a BindAsync request body as the body, key XML requests by their XML names ([b2f0349](https://github.com/atc-net/atc-rest-minimalapi/commit/b2f03498e960eea4468ffd604a0224c5da949f95))
* **validation:** use wire names as validation error keys ([9f46cb9](https://github.com/atc-net/atc-rest-minimalapi/commit/9f46cb9047ce562734a48811882389ad1ec819d7))


### Bug fixes

* **middleware:** add client ids to the request-aborted log scope ([4500f7f](https://github.com/atc-net/atc-rest-minimalapi/commit/4500f7fc33c3a4de07b26485b6617b49202d667f))
* **middleware:** resolve custom mappings by most-derived type ([7b0cf26](https://github.com/atc-net/atc-rest-minimalapi/commit/7b0cf267b70316177811c5a13cf0096d24a7fd8b))
* **middleware:** rethrow exceptions after the response has started ([7722517](https://github.com/atc-net/atc-rest-minimalapi/commit/77225175bd559cb35a5a2deb5533b5b006c975d0))
* **sample:** drop the removed integration tests from Demo.slnx and add the E2E tests ([4768bf9](https://github.com/atc-net/atc-rest-minimalapi/commit/4768bf927f9e8b104fbc0d9fefc4e966441d8aa2))
* **validation:** reject a body nested deeper than the validation depth ([21d9d3b](https://github.com/atc-net/atc-rest-minimalapi/commit/21d9d3b12a06ad78671d78d8df9d76847febd9e3))
* **validation:** return ProblemDetails when argument is not found ([425dfd8](https://github.com/atc-net/atc-rest-minimalapi/commit/425dfd8d8482e5472dbdb4ae9c5e07a4270a3912))
* **validation:** validate equal records in one request, without the MiniValidation dependency ([cc2f5f1](https://github.com/atc-net/atc-rest-minimalapi/commit/cc2f5f1b71b37ef51ce5fb3359e8d2a22f02b9cd))


### Performance improvements

* **validation:** cache [FromBody] lookup per validated type ([d38bcc1](https://github.com/atc-net/atc-rest-minimalapi/commit/d38bcc141f73739f9fa8896e20b4886ab1d76bdd))

## [2.0.41](https://github.com/atc-net/atc-rest-minimalapi/compare/v2.0.40...v2.0.41) (2026-07-23)


### Bug Fixes

* pass timeout to RemoveCollectionIndexer in ValidationProblemExtensions ([9273543](https://github.com/atc-net/atc-rest-minimalapi/commit/9273543959edd77c4a4e1bda256fcfa7635576cb))
