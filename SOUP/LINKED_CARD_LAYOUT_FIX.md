# Fix for Linked Card Layout with Odd Numbers

## Problem
When 3+ orders are linked together, they display in one long horizontal row, which gets very wide.

## Solution
Change the merged card template to use WrapPanel instead of StackPanel, so cards wrap after 2 per row.

## Location
File: `SOUP/src/Features/OrderLog/Views/OrderLogView.xaml`
Around line 411-418

## Change Needed

**FIND:**
```xml
<!-- Order Sections (Side-by-Side) -->
<ScrollViewer Grid.Row="2" HorizontalScrollBarVisibility="Auto" VerticalScrollBarVisibility="Disabled">
    <ItemsControl ItemsSource="{Binding Members}">
        <ItemsControl.ItemsPanel>
            <ItemsPanelTemplate>
                <StackPanel Orientation="Horizontal"/>
            </ItemsPanelTemplate>
        </ItemsControl.ItemsPanel>
```

**REPLACE WITH:**
```xml
<!-- Order Sections (Wrap after 2 per row for groups of 3+) -->
<ScrollViewer Grid.Row="2" HorizontalScrollBarVisibility="Auto" VerticalScrollBarVisibility="Auto">
    <ItemsControl ItemsSource="{Binding Members}">
        <ItemsControl.ItemsPanel>
            <ItemsPanelTemplate>
                <WrapPanel Orientation="Horizontal" MaxWidth="680"/>
            </ItemsPanelTemplate>
        </ItemsControl.ItemsPanel>
```

## Result
- 2 linked cards: Display side-by-side in one row (2x1)
- 3 linked cards: Display 2 in first row, 1 in second row (2+1)
- 4 linked cards: Display in 2x2 grid
- And so on...

The MaxWidth of 680 allows 2 cards of 320px width each (plus padding/margins) before wrapping.
