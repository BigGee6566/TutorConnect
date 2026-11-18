<%@ Control Language="VB" AutoEventWireup="false" CodeBehind="DashboardCard.ascx.vb"
    Inherits="TutorConnect.Controls.DashboardCard" %>

<%-- The anchor wraps the whole tile, so the entire card is the target rather than just
     the title (§F.3). --%>
<a class="dash-card" runat="server" id="cardLink">
    <span class="dash-card__icon" runat="server" id="iconWrap">
        <asp:Literal ID="litIcon" runat="server" />
        <asp:Literal ID="litCount" runat="server" />
    </span>
    <span class="dash-card__title"><asp:Literal ID="litTitle" runat="server" Mode="Encode" /></span>
    <span class="dash-card__description"><asp:Literal ID="litDescription" runat="server" Mode="Encode" /></span>
</a>
