﻿<Window x:Class="$rootnamespace$.Samples.NTestCaseBuilder.GraphDisplayWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:graphsharp="clr-namespace:GraphSharp.Controls;assembly=GraphSharp.Controls"
        xmlns:local="clr-namespace:$rootnamespace$.Samples.NTestCaseBuilder"
        xmlns:zoom="clr-namespace:WPFExtensions.Controls;assembly=WPFExtensions"        
        Title="A graph made by NTestCaseBuilder, implemented by Quick Graph and rendered by Graph Sharp..." Height="350" Width="596">

    <Window.Resources>
        <DataTemplate x:Key="magicVoodooTemplate" DataType="{x:Type local:Vertex}">
        <StackPanel Orientation="Horizontal" Margin="5">
            <TextBlock Text="{Binding Path=Id, Mode=OneWay}" Foreground="White" />
        </StackPanel>
    </DataTemplate>
        <Style TargetType="{x:Type graphsharp:VertexControl}">
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="{x:Type graphsharp:VertexControl}">
                        <Border BorderBrush="White" 
                        Background="Black"
            BorderThickness="2"
            CornerRadius="10,10,10,10"
            Padding="{TemplateBinding Padding}">
                            <ContentPresenter Content="{TemplateBinding Vertex}" 
                            ContentTemplate="{StaticResource magicVoodooTemplate}"/>

                            <Border.Effect>
                                <DropShadowEffect BlurRadius="2" Color="LightGray" 
                            Opacity="0.3" Direction="315"/>
                            </Border.Effect>
                        </Border>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
        <Style TargetType="{x:Type graphsharp:EdgeControl}">
            <Style.Resources>
                <ToolTip x:Key="ToolTipContent">
                    <StackPanel>
                        <TextBlock FontWeight="Bold" Text="Edge.Connection"/>
                        <TextBlock Text="{Binding Path=Connection}"/>
                    </StackPanel>
                </ToolTip>
            </Style.Resources>
            <Setter Property="ToolTip" Value="{StaticResource ToolTipContent}"/>
        </Style>
    </Window.Resources>
    <zoom:ZoomControl  Grid.Row="1"  Zoom="0.2" 
        ZoomBoxOpacity="0.5" Background="#ff656565">
        <local:GraphLayout x:Name="graphLayout" Margin="10"
        Graph="{Binding}"
        LayoutAlgorithmType="ISOM"
        OverlapRemovalAlgorithmType="FSA"
        HighlightAlgorithmType="Simple" />

    </zoom:ZoomControl>

</Window>