using VHSmart_Api.Shared.Domain.RawMaterial;

namespace VHSmart_Api.Features.RawMaterial.ManufacturerSupplier;

// One place where a command becomes a row, so Create and Update can never disagree about the
// mapping. Type decides which half is stored: the half that does not apply is cleared, so a
// supplier-only row never carries manufacturer values (spec 10.1 - the absent half shows N/A).
internal static class ManufacturerSupplierData
{
    public static ManufacturerSupplierEntity Apply(
        ManufacturerSupplierEntity entity,
        CreateManufacturerSupplierCommand request) =>
        Assign(
            entity, request.Type,
            request.ManufacturerName, request.ManufacturerBusinessRegNo, request.ManufacturerTypeId,
            request.ManufacturerAddress, request.ManufacturerCountryId,
            request.ManufacturerPersonInCharge, request.ManufacturerContactNo,
            request.ManufacturerEmail, request.ManufacturerWebpage,
            request.SupplierName, request.SupplierAddress, request.SupplierCountryId,
            request.SupplierPersonInCharge, request.SupplierContactNo, request.SupplierEmail);

    public static ManufacturerSupplierEntity Apply(
        ManufacturerSupplierEntity entity,
        UpdateManufacturerSupplierCommand request) =>
        Assign(
            entity, request.Type,
            request.ManufacturerName, request.ManufacturerBusinessRegNo, request.ManufacturerTypeId,
            request.ManufacturerAddress, request.ManufacturerCountryId,
            request.ManufacturerPersonInCharge, request.ManufacturerContactNo,
            request.ManufacturerEmail, request.ManufacturerWebpage,
            request.SupplierName, request.SupplierAddress, request.SupplierCountryId,
            request.SupplierPersonInCharge, request.SupplierContactNo, request.SupplierEmail);

    // Which halves the Type keeps - used by Update to test the e-mails before the row changes.
    public static bool KeepsManufacturer(ManufacturerSupplierType? type) =>
        type is ManufacturerSupplierType.ManufacturerOnly or ManufacturerSupplierType.Both;

    public static bool KeepsSupplier(ManufacturerSupplierType? type) =>
        type is ManufacturerSupplierType.SupplierOnly or ManufacturerSupplierType.Both;

    private static ManufacturerSupplierEntity Assign(
        ManufacturerSupplierEntity entity,
        ManufacturerSupplierType? type,
        string? manufacturerName,
        string? manufacturerBusinessRegNo,
        Guid? manufacturerTypeId,
        string? manufacturerAddress,
        Guid? manufacturerCountryId,
        string? manufacturerPersonInCharge,
        string? manufacturerContactNo,
        string? manufacturerEmail,
        string? manufacturerWebpage,
        string? supplierName,
        string? supplierAddress,
        Guid? supplierCountryId,
        string? supplierPersonInCharge,
        string? supplierContactNo,
        string? supplierEmail)
    {
        entity.Type = type!.Value;

        var keepManufacturer = type is ManufacturerSupplierType.ManufacturerOnly or ManufacturerSupplierType.Both;
        var keepSupplier = type is ManufacturerSupplierType.SupplierOnly or ManufacturerSupplierType.Both;

        entity.ManufacturerName = keepManufacturer ? manufacturerName : null;
        entity.ManufacturerBusinessRegNo = keepManufacturer ? manufacturerBusinessRegNo : null;
        entity.ManufacturerTypeId = keepManufacturer ? manufacturerTypeId : null;
        entity.ManufacturerAddress = keepManufacturer ? manufacturerAddress : null;
        entity.ManufacturerCountryId = keepManufacturer ? manufacturerCountryId : null;
        entity.ManufacturerPersonInCharge = keepManufacturer ? manufacturerPersonInCharge : null;
        entity.ManufacturerContactNo = keepManufacturer ? manufacturerContactNo : null;
        entity.ManufacturerEmail = keepManufacturer ? manufacturerEmail : null;
        entity.ManufacturerWebpage = keepManufacturer ? manufacturerWebpage : null;

        entity.SupplierName = keepSupplier ? supplierName : null;
        entity.SupplierAddress = keepSupplier ? supplierAddress : null;
        entity.SupplierCountryId = keepSupplier ? supplierCountryId : null;
        entity.SupplierPersonInCharge = keepSupplier ? supplierPersonInCharge : null;
        entity.SupplierContactNo = keepSupplier ? supplierContactNo : null;
        entity.SupplierEmail = keepSupplier ? supplierEmail : null;

        return entity;
    }
}
