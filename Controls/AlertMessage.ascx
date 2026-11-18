<%@ Control Language="VB" AutoEventWireup="false" CodeBehind="AlertMessage.ascx.vb"
    Inherits="TutorConnect.Controls.AlertMessage" %>

<%-- role is set from code-behind: "alert" for errors so it interrupts a screen reader,
     "status" for success and info so it does not. --%>
<asp:Panel ID="pnlAlert" runat="server" Visible="false">
    <asp:Literal ID="litMessage" runat="server" Mode="Encode" />
</asp:Panel>
