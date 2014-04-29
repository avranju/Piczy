<%@ Page Title="Piczy" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Default.aspx.cs" Inherits="Piczy.Web._Default" %>

<asp:Content runat="server" ID="BodyContent" ContentPlaceHolderID="MainContent">
    <script src="/Scripts/default.js"></script>
    <table id="hor-minimalist-a">
        <thead>
            <tr>
                <th>Preview</th>
                <th>Image Name</th>
                <th>Size</th>
                <th>Status</th>
            </tr>
        </thead>
        <tbody id="tableBody">
            <tr id="no-items-msg">
                <td colspan="4" style="padding-bottom: 8px; margin-top: 4px;">Nothing to see here. Upload some photos!</td>
            </tr>
        </tbody>
    </table>
    <br />
    <button id="btnUpload">Upload Photos</button>
    <input type="file" name="pics" id="pics" multiple accept="image/gif, image/jpeg, image/png" style="visibility: hidden;" />
</asp:Content>
