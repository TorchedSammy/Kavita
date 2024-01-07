import { ChangeDetectionStrategy, Component } from '@angular/core';
import {ImageComponent} from '../../../shared/image/image.component';

@Component({
    selector: 'app-splash-container',
    templateUrl: './splash-container.component.html',
    styleUrls: ['./splash-container.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
    imports: [ImageComponent]
})
export class SplashContainerComponent {}