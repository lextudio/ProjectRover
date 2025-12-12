using System;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Interactivity;
using Avalonia;
using ProjectRover.ViewModels;

namespace ProjectRover.Views;

public class AssemblyCandidateChooserWindow : Window
{
    private readonly ListBox listBox;
    private readonly Button okButton;

    public AssemblyCandidateChooserWindow()
    {
        Width = 600;
        Height = 300;
        Title = "Select Assembly Candidate";

        var stack = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(8) };
        listBox = new ListBox { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch }; 
        okButton = new Button { Content = "OK", HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0,8,0,0) };
        okButton.Click += OkButton_Click;

        stack.Children.Add(listBox);
        stack.Children.Add(okButton);

        Content = stack;
    }

    private void OkButton_Click(object? sender, RoutedEventArgs e)
    {
        Close(listBox.SelectedItem as string);
    }

    public void SetViewModel(AssemblyCandidateChooserViewModel vm)
    {
        DataContext = vm;
        listBox.ItemsSource = vm.Candidates;
        listBox.SelectedItem = vm.Selected;
    }
}
