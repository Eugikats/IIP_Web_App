# Academy pricing: USD 120 per course

All ten catalogue courses, including Claims Communication & Service Recovery, default to USD 120. Existing saved workspace prices remain editable and are not silently overwritten by a deployment. The live Academy operator's price list must also be updated through the authorised pricing endpoint to apply this release to the public catalogue and new payment quotes.

Use GET and PUT /api/v1/academy/pricing as an authorised settings administrator in the Academy operator workspace. Preserve the prior response before updating every catalogue course to 120, then verify the public catalogue and each course detail. Changes are audited by the existing pricing service. Do not update another insurer workspace or rewrite historical payment orders.

This release does not enable payment gateways, approve reviewer courses for sale, change enrolment or assessment policy, or apply database migrations. Deploy the API only; the portal already reads prices from it.

Rollback: restore the prior operator price list through the same endpoint and redeploy the previous API package if its health or catalogue checks regress.
